\ Copyright (c) 2023-2024 Travis Bemann
\ Copyright (c) 2024 Serialcomms (GitHub)
\
\ Permission is hereby granted, free of charge, to any person obtaining a copy
\ of this software and associated documentation files (the "Software"), to deal
\ in the Software without restriction, including without limitation the rights
\ to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
\ copies of the Software, and to permit persons to whom the Software is
\ furnished to do so, subject to the following conditions:
\ 
\ The above copyright notice and this permission notice shall be included in
\ all copies or substantial portions of the Software.
\ 
\ THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
\ IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
\ FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
\ AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
\ LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
\ OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
\ SOFTWARE.

compile-to-flash

begin-module usb-core

  armv6m import
  task import
  interrupt import
  
  usb-constants import

  variable IRQ_counter

  \ Device configured callback
  variable usb-device-configured-callback
  
  \ Prepare to set device as configured
  variable prepare-usb-device-configured?

  \ Device configuration set
  variable usb-device-configured?
  
  : debug ( xt -- )
    emit-hook @ { saved-emit-hook }
    emit?-hook @ { saved-emit?-hook }
    flush-console-hook @ { saved-flush-console-hook }
    pause-hook @ { saved-pause-hook }
    ['] internal::serial-emit emit-hook !
    ['] internal::serial-emit? emit?-hook !
    [: ;] flush-console-hook !
    [: ;] pause-hook !
    try
    saved-emit-hook emit-hook !
    saved-emit?-hook emit?-hook !
    saved-flush-console-hook flush-console-hook !
    saved-pause-hook pause-hook !
    ?raise
  ;

  \ USB unknown setup request
  : x-usb-unknown-request ( -- )
    [ debug? ] [if]
      [: ." Unknown USB Request ... " cr ;] debug
    [then]
  ;
  
  \ USB unknown setup request type
  : x-usb-unknown-req-type ( -- )
    [ debug? ] [if]
      [: ." Unknown USB Request Type... " cr ;] debug
    [then]
  ;
  
  \ USB unknown setup recipient
  : x-usb-unknown-recipient ( -- )
    [ debug? ] [if]
      [: ." Unknown USB Recipient ... " cr ;] debug
    [then]
  ;
  
  \ USB unknown descriptor
  : x-usb-unknown-descriptor ( -- )
    [ debug? ] [if]
      [: ." Unknown USB Descriptor Type ... " cr ;] debug
    [then]
  ;
                                                     
  : usb-buffer-offset ( addr -- addr' ) USB_DPRAM_Base - ;                      \ USB dual-ported RAM offset

  : reset-usb-hardware ( -- )                                                   \ Reset Pico USB hardware peripheral       
    RESETS_USBCTRL RESETS_RESET_Set !
    RESETS_USBCTRL RESETS_RESET_Clr !
    begin RESETS_RESET_DONE @ not RESETS_USBCTRL and while repeat
  ;

  : ENDPOINT_CONTROL_TO_HOST ( endpoint -- address )     \ USB endpoint in endpoint control
    3 lshift USB_DPRAM_Base +
  ;

  : ENDPOINT_CONTROL_TO_PICO ( endpoint -- address )     \ USB endpoint out endpoint control
    3 lshift [ USB_DPRAM_Base cell+ ] literal +
  ;
    
  : BUFFER_CONTROL_TO_HOST ( endpoint -- address )       \ USB endpoint in buffer control
    3 lshift [ USB_DPRAM_Base $80 + ] literal +
  ;

  : BUFFER_CONTROL_TO_PICO ( endpoint -- address )      \ USB endpoint out buffer control
    3 lshift [ USB_DPRAM_Base $80 + cell+ ] literal +
  ;

    create device-data

    $12 c, $01 c,                                                       \ USB_DEVICE_DESCRIPTOR
    $00 c, $02 c,                                                       \ USB_VERSION_BCD                                             
    $EF c, $02 c, $01 c,                                                \ USB_MISC_IAD_DEVICE (IAD = Interface Association Device)
    $40 c,                                                              \ USB_EP0_MAX
    $83 c, $04 c,                                                       \ USB_VENDOR_ID
    $40 c, $57 c,                                                       \ USB_PRODUCT_ID 
    $00 c, $02 c,                                                       \ USB_PRODUCT_BCD 
    $00 c,                                                              \ STRING_MANUFACTURER (0 = none) 
    $00 c,                                                              \ STRING_PRODUCT      (0 = none)
    $00 c,                                                              \ STRING_SERIAL       (0 = none)
    $01 c,                                                              \ CONFIGURATIONS

    here device-data - cell align, constant device-data-size

    create config-data

    $09 c, $02 c, $4B c, $00 c, $02 c, $01 c, $00 c, $80 c, $FA c,      \ Configuration Descriptor ( 75 bytes )
    $08 c, $0B c, $00 c, $02 c, $02 c, $02 c, $00 c, $00 c,             \ Interface Association Descriptor (IAD)
    $09 c, $04 c, $00 c, $00 c, $01 c, $02 c, $02 c, $00 c, $00 c,      \ CDC Class Interface Descriptor (CCI)
    $05 c, $24 c, $00 c, $20 c, $01 c,                                  \ CDC Functional Descriptor - Header (CDC Class revision 1.20)
    $04 c, $24 c, $02 c, $02 c,                                         \ CDC Functional Descriptor - Abstract Control Management
    $05 c, $24 c, $06 c, $00 c, $01 c,                                  \ CDC Functional Descriptor - Union
    $05 c, $24 c, $01 c, $01 c, $00 c,                                  \ CDC Functional Descriptor - Call Management
    $07 c, $05 c, $83 c, $03 c, $10 c, $00 c, $01 c,                    \ Endpoint Descriptor: EP3 In - (max packet size = 16)

    $09 c, $04 c, $01 c, $00 c, $02 c, $0A c, $00 c, $00 c, $00 c,      \ CDC Data Class Interface Descriptor: CDC Data
    $07 c, $05 c, $81 c, $02 c, $40 c, $00 c, $00 c,                    \ Endpoint Descriptor: EP1 In  - Bulk Transfer Type
    $07 c, $05 c, $01 c, $02 c, $40 c, $00 c, $00 c,                    \ Endpoint Descriptor: EP1 Out - Bulk Transfer Type

    here config-data - cell align, constant config-data-size

    create string-data

    $04 c, $03 c, $09 c, $04 c,                                         \ Language String Descriptor LANGUAGE_ENGLISH_US

    here string-data - cell align, constant string-data-size

    begin-structure line-state-notification-descriptor

      cfield: line-request-type
      cfield: line-request
      hfield: line-value
      hfield: line-index
      hfield: line-length
      hfield: line-data
    
    end-structure

    begin-structure usb-endpoint-profile

      field: tx?                  \ Is endpoint transmit to host (USB Direction = In)
      field: ack?                 \ Send ACK on buffer completion (control transfers)  
      field: busy?                \ Is endpoint currently active tx/rx ? (As set by Forth - not USB hardware)
      field: number               \ Endpoint Number, for debug etc.            
      field: max-packet-size      \ Endpoint maximum packet size
      field: dpram-address        \ DPRAM address in Pico hardware
      field: next-pid             \ Endpoint next PID (0 for PID0 or 8192 for PID1)
      field: buffer-control       \ Endpoint buffer control register address
      field: endpoint-control     \ Endpoint control register address
      field: transfer-type        \ Endpoint transfer type (Control, Bulk, Interrupt)
      field: transfer-bytes       \ Endpoint transfer bytes (To send or received)
      field: processed-bytes      \ Endpoint processed bytes (Multipacket)
      field: pending-bytes        \ Endpoint bytes pending (Multipacket)
      field: total-bytes          \ Total transfer bytes (Multipacket)
      field: source-address       \ Source data address (Multipacket transmit)
      field: callback-handler     \ Callback handler for CDC set line coding 

    \ there is no endpoint control register for EP0, interrupt enable for EP0 comes from SIE_CTRL

    end-structure

    begin-structure usb-setup-command

      cfield: setup-descriptor-type
      cfield: setup-descriptor-index
      cfield: setup-request-type        \ Setup packet request type     
      cfield: setup-direction?          \ Setup direction ( &80 = to Host)
      cfield: setup-recipient           \ Setup Recipient ( device, interface, endpoint )
      cfield: setup-request             \ Setup packet request
      hfield: setup-length              \ Setup packet length
      hfield: setup-value               \ Setup packet value
      hfield: setup-index               \ Setup packet index
    
    end-structure

    begin-structure cdc-line-coding-profile

      field: cdc-line-baud
      cfield: cdc-line-parity
      cfield: cdc-line-stop
      cfield: cdc-line-data

    end-structure

    usb-setup-command buffer: usb-setup

    cdc-line-coding-profile buffer: cdc-line-coding

    line-state-notification-descriptor buffer: line-state-notification

    : init-cdc-line-coding ( -- )

      115200  cdc-line-coding cdc-line-baud !
      0       cdc-line-coding cdc-line-parity c! 
      0       cdc-line-coding cdc-line-stop c! 
      8       cdc-line-coding cdc-line-data c!       
    
    ;

    : init-line-nofification ( -- )
    
      $A1 line-state-notification line-request c!
      $20 line-state-notification line-request-type c!
        0 line-state-notification line-value h!
        0 line-state-notification line-index h!
        2 line-state-notification line-length h!
        0 line-state-notification line-data h!

      \ $A1 c, $20 c, $00 c, $00 c, $00 c, $00 c, $02 c, $00 c, $00 c, $00 c,            \ Line State Notification
    
    ;

    usb-endpoint-profile buffer: EP0-to-Host    \ Default endpoint 0 to Host
    usb-endpoint-profile buffer: EP0-to-Pico    \ Default endpoint 0 to Pico
    usb-endpoint-profile buffer: EP1-to-Host    \ Function endpoint 1 to Host
    usb-endpoint-profile buffer: EP1-to-Pico    \ Function endpoint 1 to Pico
    usb-endpoint-profile buffer: EP3-to-Host    \ Function endpoint 3 to Host
 
  : debug-separator ( -- )

    [ debug? ] [if]
      ." -------------------------------------------------------------------------------------"  cr
    [then]
  
  ;

  : usb-init-setup-packet ( -- )

    usb-setup usb-setup-command 0 fill
  ;

  : init-ep-common ( ep-max ep-tx? ep-number endpoint -- )
    [: { ep-max ep-tx? ep-number endpoint }

      endpoint usb-endpoint-profile 0 fill

      ep-tx? endpoint tx? !
      
      ep-number endpoint number !
      
      ep-max endpoint max-packet-size !

      ep-tx? if

        ep-number BUFFER_CONTROL_TO_HOST else 
        ep-number BUFFER_CONTROL_TO_PICO 
        
      then endpoint buffer-control !

      0 endpoint buffer-control @ ! 

      [ debug? ] [if]
        
        debug-separator
        
        cr

        ." Initialising Endpoint " ep-number h.8 ." to " ep-tx? if ." Host " else ." Pico " then
        
        ." (common), max packet size = $" ep-max h.8 cr
        
        ." Buffer Control Address = $" endpoint buffer-control @ h.8 cr

      [then]

    ;] debug
  ;

  : init-usb-endpoint-0 ( ep-max ep-tx? endpoint -- )
    [: { ep-max ep-tx? endpoint }
      
      ep-max ep-tx? 0     endpoint init-ep-common
      USB_EP0_BUFFER      endpoint dpram-address !
      USB_EP_TYPE_CONTROL endpoint transfer-type !

      [ debug? ] [if]
        ." EP Buffer <shared> = $" USB_EP0_BUFFER h.8 cr
      [then]
      
    \ there is no endpoint control register for EP0, interrupt enable for EP0 comes from SIE_CTRL
      
    ;] debug
  ;

  : init-ep-x-to-host ( ep-type ep-number endpoint -- )
    [: { ep-type ep-number endpoint }
      
      128 ep-number 1 - * { dpram-offset }

      $0180 dpram-offset + { ep-dpram-address } \ first half of dpram

      ep-type EP_CTRL_BUFFER_TYPE_LSB lshift { ep-control }

      USB_EP_ENABLE_INTERRUPT_PER_BUFFER ep-control or to ep-control

      USB_EP_ENABLE ep-control or to ep-control

      ep-dpram-address ep-control or to ep-control

      ep-control ep-number ENDPOINT_CONTROL_TO_HOST !

      ep-dpram-address $5010_0000 + endpoint dpram-address !

      [ debug? ] [if]
        cr ." Initialising Endpoint " ep-number h.8 ." to Host EP Buffer = $" 
        $5010_0000 ep-dpram-address + h.8 ." , EP Control = $" ep-control h.8 cr
      [then]
      
    ;] debug
   ;

   : init-ep-x-to-pico ( ep-type ep-number endpoint -- )
     [: { ep-type ep-number endpoint }

       128 ep-number 1 - * { dpram-offset }

       $0900 dpram-offset + { ep-dpram-address } \ second half of dpram

       ep-type EP_CTRL_BUFFER_TYPE_LSB lshift { ep-control }

       USB_EP_ENABLE_INTERRUPT_PER_BUFFER ep-control or to ep-control

       USB_EP_ENABLE ep-control or to ep-control

       ep-dpram-address ep-control or to ep-control

       ep-control ep-number ENDPOINT_CONTROL_TO_PICO !

       ep-dpram-address $5010_0000 + endpoint dpram-address !

       [ debug? ] [if]
         cr ." Initialising Endpoint " ep-number h.8 ." to Pico EP Buffer = $" 
         $5010_0000 ep-dpram-address + h.8 ." , EP Control = $" ep-control h.8 cr
       [then]
     ;] debug
   ;

  : init-usb-endpoint-x { ep-max ep-tx? ep-type ep-number endpoint -- }
    ep-type endpoint transfer-type !
    ep-max ep-tx? ep-number endpoint init-ep-common
    ep-type ep-number endpoint 
    ep-tx? if init-ep-x-to-host else init-ep-x-to-pico then
  ;
  
  \ Initialize USB default endpoints 0
  : init-usb-default-endpoints ( -- )
    64 true  EP0-to-Host init-usb-endpoint-0
    64 false EP0-to-Pico init-usb-endpoint-0
  ;

  \ Initialize CDC/ACM Function Endpoints
  : init-usb-function-endpoints ( -- )
    64 true  USB_EP_TYPE_BULK       1 EP1-to-Host init-usb-endpoint-x
    64 false USB_EP_TYPE_BULK       1 EP1-to-Pico init-usb-endpoint-x
    16 true  USB_EP_TYPE_INTERRUPT  3 EP3-to-Host init-usb-endpoint-x
  ;

  : usb-toggle-data-pid ( endpoint -- )
    [: { endpoint }

      [ debug? ] [if]
        ." Toggling data packet ID, Old PID = $" endpoint next-pid @ h.8 cr
      [then]
  
      endpoint next-pid @ if
        USB_BUF_CTRL_DATA0_PID
      else
        USB_BUF_CTRL_DATA1_PID
      then endpoint next-pid !

      [ debug? ] [if]
        ." Toggling data packet ID, New PID = $" endpoint next-pid @ h.8 cr
      [then]

    ;] debug
  ;

  : show-ep0-dpram ( -- )
    EP0-to-Host dpram-address @ EP0-to-Host dpram-address @ 16 + dump
  ;

  : show-ep0-values ( endpoint -- )
    [: { endpoint } 
      [ debug? ] [if]
        ." USB Endpoint 0, dpram address = $" endpoint dpram-address @ h.8 cr
      [then]
    ;] debug
  ;

  : usb-set-buffer-available { endpoint -- }
    code[ b> >mark b> >mark b> >mark b> >mark b> >mark b> >mark ]code
    USB_BUF_CTRL_AVAIL endpoint buffer-control @ bis!
  ;

  : usb-dispatch-buffer { endpoint -- }
    USB_BUF_CTRL_FULL endpoint buffer-control @ bis!
    endpoint usb-set-buffer-available
  ;

  : usb-receive-zero-length-packet ( endpoint -- )
    [: { endpoint }
      [ debug? ] [if]
        ." USB Receive Zero Length Packet, PID = $" endpoint next-pid @ h.8 cr
      [then]
    
      endpoint next-pid @ endpoint buffer-control @ ! 
      endpoint usb-set-buffer-available   
      
    ;] debug
  ;

  : usb-send-zero-length-packet ( endpoint -- )
    [: { endpoint }
      [ debug? ] [if]
        ." USB Send Zero Length Packet, PID = $" endpoint next-pid @ h.8 cr
      [then]
      
      endpoint next-pid @ USB_BUF_CTRL_FULL or endpoint buffer-control @ !
      endpoint usb-set-buffer-available  
      
    ;] debug
  ;

  : usb-send-data-packet ( endpoint bytes -- )
    [: { endpoint bytes }

      true endpoint busy? !

      [ debug? ] [if]
        ." USB Send Data Packet, Endpoint = $" endpoint number @ h.8 ." , bytes = $" bytes h.8 cr
      [then]
    
      endpoint next-pid @ bytes or endpoint buffer-control @ ! 
      endpoint usb-dispatch-buffer
      
    ;] debug
  ;

  : usb-receive-data-packet ( endpoint bytes -- )
    [: { endpoint bytes }

      true endpoint busy? !
      
      [ debug? ] [if]
        ." USB Receive Data Packet, Endpoint = $" endpoint number @ h.8 ." , bytes = $" bytes h.8 cr
      [then]
  
      endpoint next-pid @ bytes or endpoint buffer-control @ ! 
      USB_BUF_CTRL_AVAIL endpoint buffer-control @ bis!
      
    ;] debug
  ;

  \ Send a USB acknowledge out request (zero length packet)
  : usb-ack-out-request ( -- )
    EP0-to-Host usb-send-zero-length-packet
  ;

  \ Send a stall on an endpoint
  : usb-control-send-stall ( -- )
    USB_BUF_CTRL_STALL EP0-to-Host buffer-control @ !
    1 USB_EP_STALL_ARM !
  ;
  
  : usb-get-device-address ( -- )
    USB_DEVICE_ADDRESS @ $1F and 
  ;

  \ Set USB hardware device address
  : usb-set-device-address ( -- )
    [:
      
      usb-ack-out-request 

      [ debug? ] [if]
        ." USB Set Pico New Device Address = $" usb-setup setup-value h@ h.8 cr
      [then]
      
      1000. timer::delay-us
      
      usb-setup setup-value h@ USB_DEVICE_ADDRESS !
      
    ;] debug
  ;

  : usb-build-data-packet ( endpoint bytes source-data -- )
    [: { endpoint bytes source-data }

      [ debug? ] [if]
        debug-separator
        
        ." USB Build Data Packet, Source Address = $" source-data h.8
        ." , Bytes = $" bytes .
        ." , Destination Address = $" endpoint dpram-address @ h.8 cr
      [then]
      
      \ do not exceed max packet size
      endpoint max-packet-size @ bytes min { packet-bytes }
      
      source-data endpoint dpram-address @ packet-bytes move
      
    ;] debug
  ;

  : usb-start-transfer-to-host
    ( host-endpoint total-data-bytes source-data-address -- )
    [: { host-endpoint total-data-bytes source-data-address }

      [ debug? ] [if]
        debug-separator
        
        ." USB Start Transfer to Host, "
        ." Endpoint Address=" host-endpoint dpram-address @ ." $" h.8
        ." , Source Address=" source-data-address ." $" h.8
        ." , Total Bytes=" total-data-bytes h.8 cr
      [then]
      
      total-data-bytes host-endpoint total-bytes !
      source-data-address host-endpoint source-address !
      
      \ do not exceed max packet size
      host-endpoint max-packet-size @ total-data-bytes min { packet-bytes }
      
      host-endpoint packet-bytes source-data-address usb-build-data-packet
      host-endpoint packet-bytes usb-send-data-packet
      
    ;] debug
  ;
    
  : usb-start-control-transfer-to-host
    ( total-data-bytes source-data-address -- )
    [: { total-data-bytes source-data-address }

      [ debug? ] [if]
        debug-separator
        
        ." USB Start Control Transfer to Host, Pico Device Address = $" usb-get-device-address h.8 cr
        ." Source Address=" source-data-address ." $" h.8
        ." , Total Bytes=" total-data-bytes .
        ." , Setup Length=" usb-setup setup-length h@ .
        ." , Stack Depth=" depth h.8 cr
      [then]
      
      0 EP0-to-Host transfer-bytes !
      0 EP0-to-Host processed-bytes !
      total-data-bytes EP0-to-Host total-bytes !
      total-data-bytes EP0-to-Host pending-bytes !
      
      usb-get-device-address if
        
        true EP0-to-Host ack? !
        
        EP0-to-Host total-data-bytes source-data-address usb-start-transfer-to-host
        
      else

        [ debug? ] [if]
          ." Control Transfer, Device Address = 0" cr
        [then]
        
        false EP0-to-Host ack? !
        EP0-to-Host total-data-bytes source-data-address usb-start-transfer-to-host
        EP0-to-Pico usb-receive-zero-length-packet 

      then

    ;] debug
  ;

  : usb-end-control-transfer-to-host ( -- )
    [:

      [ debug? ] [if]
        ." USB Ending Control Transfer to Host " cr
      [then]
      
      EP0-to-Pico usb-receive-zero-length-packet
      
      false EP0-to-Host ack? !
      false EP0-to-Host busy? !
      
    ;] debug
  ;
  
  : usb-send-device-descriptor ( -- )
    [:

      [ debug? ] [if]
        debug-separator
        
        ." USB Send Device Descriptor, Length = $" usb-setup setup-length h@ h.8 cr
      [then]
      
      usb-setup setup-length h@ device-data-size = if
        device-data-size
      else
        8 
      then
      device-data usb-start-control-transfer-to-host 
      
    ;] debug
  ;
    
  : usb-send-config-descriptor ( -- )
    [:

      [ debug? ] [if]
        debug-separator
        
        ." USB Send Configuration Descriptor, Length = $" usb-setup setup-length h@ h.8 cr
      [then]
      
      usb-setup setup-length h@ config-data-size = if 
        config-data-size
      else
        9 
      then
      config-data usb-start-control-transfer-to-host     
      
    ;] debug
  ;

  : usb-send-string-descriptor ( -- )
    [:

      [ debug? ] [if]
        ." USB Send String Descriptors, <none> , Index = $" usb-setup setup-index h@ h.8 cr
      [then]

      usb-setup setup-index h@ 0 = if
        4 string-data usb-start-control-transfer-to-host
      else
        usb-ack-out-request
      then

    ;] debug
  ;

  \ Set USB device configuration 1
  : usb-set-device-configuration ( -- )
    [:

      [ debug? ] [if]
        ." USB Set Device Configuration, Initialising Function Endpoints ... " cr
      [then]
      
      init-usb-function-endpoints
      init-line-nofification 
      init-cdc-line-coding
      8192 EP0-to-Host next-pid !
      EP3-to-Host 10 line-state-notification usb-start-transfer-to-host 
      true prepare-usb-device-configured? !
      usb-ack-out-request
      
    ;] debug
  ;

  : usb-setup-type-standard-in ( -- )
    [:

      [ debug? ] [if]
        debug-separator
        
        ." USB Setup Type Standard [IN] Handler, Request = $" usb-setup setup-request c@ .
        ." , Descriptor Type = $" usb-setup setup-descriptor-type c@ h.8 cr
      [then]
      
      usb-setup setup-request c@ case
        USB_REQUEST_GET_DESCRIPTOR of
          usb-setup setup-descriptor-type c@ case
            USB_DT_DEVICE of usb-send-device-descriptor endof
            USB_DT_CONFIG of usb-send-config-descriptor endof
            USB_DT_STRING of usb-send-string-descriptor endof
            \ x-usb-unknown-descriptor
            \ usb-ack-out-request
            usb-control-send-stall
          endcase 
        endof
        \ usb-ack-out-request
        usb-control-send-stall
      endcase
    ;] debug
  ;

  : usb-setup-type-standard-out ( -- )
    [:

      [ debug? ] [if]
        debug-separator
        
        ." USB Setup Type Standard [OUT] Handler, Request = $" usb-setup setup-request c@ h.8 cr
      [then]
      
      usb-setup setup-request c@ case
        USB_REQUEST_SET_ADDRESS of usb-set-device-address endof
        USB_REQUEST_SET_CONFIGURATION of usb-set-device-configuration endof
        \ usb-ack-out-request
        usb-control-send-stall
      endcase
    ;] debug
  ;


  : usb-setup-type-standard ( -- )

    usb-setup setup-direction? c@ if
      usb-setup-type-standard-in
    else
      usb-setup-type-standard-out
    then
  ;

  : usb-class-get-line-coding ( -- )
    [:

      [ debug? ] [if]
        ." USB Setup Class, Get Line Coding " cr
      [then]

      7 cdc-line-coding usb-start-control-transfer-to-host    
      
    ;] debug
  ;

  : usb-class-set-line-coding ( -- )
    [:

      [ debug? ] [if]
        ." USB Setup Class, Set Line Coding " cr
      [then]
      
      1 EP0-to-Pico callback-handler !
      EP0-to-Pico 7 usb-receive-data-packet 
      
    ;] debug
  ;

  : usb-class-set-line-control ( -- )
    [:

      [ debug? ] [if]
        ." USB Setup Class, Set Line Control " cr
      [then]
      
      usb-setup setup-value h@ line-state-notification line-value h!
      usb-ack-out-request
      
    ;] debug
  ;

  : usb-setup-type-class ( -- )

    usb-setup setup-request c@ case
      CDC_CLASS_SET_LINE_CODING of usb-class-set-line-coding endof
      CDC_CLASS_GET_LINE_CODING of usb-class-get-line-coding endof
      CDC_CLASS_SET_LINE_CONTROL of usb-class-set-line-control endof
      \ usb-ack-out-request
      usb-control-send-stall
    endcase
  ;
  
  : usb-show-setup-packet ( -- )
    [:

      [ debug? ] [if]
        ." USB Setup Packet Handler, Bytes =    " 
        USB_SETUP_PACKET USB_SETUP_PACKET 8 + dump
        cr
      [then]

      ;] debug
  ;

  : usb-prepare-setup-packet ( -- )
    [:

      \ [ debug? ] [if]
      \   cr ." USB Preparing Setup Packet  ... "
      \ [then]

      USB_SETUP_PACKET 1 + c@ usb-setup setup-request c!
      USB_SETUP_PACKET 2 + c@ usb-setup setup-descriptor-index c!
      USB_SETUP_PACKET 3 + c@ usb-setup setup-descriptor-type c!
      
      USB_SETUP_PACKET 0 + c@ $80 and usb-setup setup-direction? c!
      USB_SETUP_PACKET 0 + c@ $1f and usb-setup setup-recipient c!
      USB_SETUP_PACKET 0 + c@ $60 and usb-setup setup-request-type c!
      
      USB_SETUP_PACKET 3 + c@ 8 lshift USB_SETUP_PACKET 2 + c@ or usb-setup setup-value h!
      USB_SETUP_PACKET 5 + c@ 8 lshift USB_SETUP_PACKET 4 + c@ or usb-setup setup-index h!
      USB_SETUP_PACKET 7 + c@ 8 lshift USB_SETUP_PACKET 6 + c@ or usb-setup setup-length h!

      [ debug? ] [if]
        \ debug-separator
        
        ." USB Setup Packet, Request Type=" usb-setup setup-request-type c@ h.8 ." , "
        ." Direction=" usb-setup setup-direction? c@ h.8 ." , "
        ." Request=" usb-setup setup-request c@ h.8 ." , " cr
        ." Recipient=" usb-setup setup-recipient c@ h.8 ." , "
        ." Value=" usb-setup setup-value h@ h.8 ." , "
        ." Index=" usb-setup setup-index h@ h.8 ." , "
        ." Length=" usb-setup setup-length h@ h.8 cr
      [then]
      
    ;] debug
  ;

  : usb-setup-packet-debug ( -- )
    [ debug? ] [if]
      debug-separator
      usb-show-setup-packet
    [then]
  ;

  : usb-prepare-setup-direction ( -- )
    [:
      
      usb-setup setup-direction? c@ if
        
        USB_BUF_CTRL_AVAIL   EP0-to-Host buffer-control @ bis!
        USB_BUF_CTRL_AVAIL   EP0-to-Pico buffer-control @ bic!
        
      else
        
        USB_BUF_CTRL_AVAIL   EP0-to-Host buffer-control @ bic!
        USB_BUF_CTRL_AVAIL   EP0-to-Pico buffer-control @ bis!
        
      then

      [ debug? ] [if]
        ." EP 0 Buffer Control Address to Host = $" EP0-to-Host buffer-control @ h.8 cr
        ." EP 0 Buffer Control Address to Pico = $" EP0-to-Pico buffer-control @ h.8 cr
      [then]
      
    ;] debug
  ;

  : usb-handle-setup-packet ( -- )

    USB_SIE_STATUS_SETUP_REC USB_SIE_STATUS !

    [ debug? ] [if]
      debug-separator
    [then]

    usb-prepare-setup-packet
    usb-prepare-setup-direction
    usb-setup-packet-debug
    USB_BUF_CTRL_DATA1_PID EP0-to-Host next-pid ! 
    USB_BUF_CTRL_DATA1_PID EP0-to-Pico next-pid !

    usb-setup setup-request-type c@ case 
      USB_REQUEST_TYPE_STANDARD of usb-setup-type-standard endof
      USB_REQUEST_TYPE_CLASS of usb-setup-type-class endof

      \ x-usb-unknown-req-type
      \ usb-ack-out-request
      usb-control-send-stall

    endcase
  ;

  : usb-bus-reset-debug ( -- )
    [:

      [ debug? ] [if]
        debug-separator
        
        ." USB Bus Reset Handler " cr
        
        \    0 flush-uart
        
        1000. timer::delay-us
      [then]
      
    ;] debug
  ;
    
  : usb-handle-bus-reset ( -- ) \ Handle bus reset
    USB_SIE_STATUS_BUS_RESET USB_SIE_STATUS !
    usb-bus-reset-debug 
    0 USB_DEVICE_ADDRESS !

    false usb-device-configured? !
    false prepare-usb-device-configured? !
  ;

  : usb-update-endpoint-byte-counts { endpoint -- }
    endpoint buffer-control @ @ USB_BUF_CTRL_LEN_MASK and endpoint transfer-bytes !
    endpoint processed-bytes @ endpoint transfer-bytes @ + endpoint processed-bytes ! 
    endpoint total-bytes @ endpoint processed-bytes @ - endpoint pending-bytes !
  ;
  
  : usb-get-continue-source-address { endpoint -- }
    endpoint source-address @ endpoint processed-bytes @ +  
  ;

  : usb-get-next-packet-size-to-host { endpoint -- }
    endpoint pending-bytes @ endpoint max-packet-size @ min 0 max  \ check against negative values
  ;

  : ep0-handler-to-host ( -- )
    [:

      \ Write to clear
      USB_BUFFER_STATUS_EP0_TO_HOST USB_BUFFER_STATUS bis!

      [ debug? ] [if]
        ." EP0 Handler to Host, Last PID = $" EP0-to-Host next-pid @ h.8 cr
      [then]
      
      EP0-to-Host usb-toggle-data-pid 
      EP0-to-Host usb-update-endpoint-byte-counts
      EP0-to-Host usb-get-next-packet-size-to-host { next-packet-size }

      [ debug? ] [if]
        ." EP0 Handler to Host, Bytes Transferred = $" EP0-to-Host transfer-bytes @ h.8 
        ." , Bytes Processed = $" EP0-to-Host processed-bytes @ h.8
        ." , Bytes Pending = $" EP0-to-Host pending-bytes @ h.8 
        ." , Next Packet Bytes = $" next-packet-size h.8 cr
      [then]

      next-packet-size if

        EP0-to-Host usb-get-continue-source-address { continue-source-address }

        [ debug? ] [if]
          ." EP0 Handler to Host, Continuing Transfer, Next Packet Size = $" next-packet-size h.8 cr
          ." Original source address = $" EP0-to-Host source-address @ h.8 cr
          ." Continue source address = $" continue-source-address h.8 cr
        [then]
        
        EP0-to-Host next-packet-size continue-source-address usb-build-data-packet
        EP0-to-Host next-packet-size usb-send-data-packet
        
      else

        EP0-to-Host ack? @ if usb-end-control-transfer-to-host then

        [ debug? ] [if]
          ." EP0 Handler to Host, Complete " cr
        [then]

        prepare-usb-device-configured? @ if
          false prepare-usb-device-configured? !
          true usb-device-configured? !

          [ debug? ] [if]
            ." USB device configured! " cr
          [then]

          usb-device-configured-callback @ ?execute
        then
        
      then
      
    ;] debug
  ;

  : ep0-handler-to-pico-callback ( -- )
    EP0-to-Pico dpram-address @ cdc-line-coding 7 move
    0 EP0-to-Pico callback-handler ! 
    usb-ack-out-request
  ;

  : ep0-handler-to-pico ( -- )
    [:
      
      [ debug? ] [if]
        ." EP0 Handler to Pico, Last PID = $" EP0-to-Pico next-pid @ h.8 cr
      [then]
      
      \ Write to clear
      USB_BUFFER_STATUS_EP0_TO_PICO USB_BUFFER_STATUS bis!
      
      EP0-to-Pico usb-toggle-data-pid 
      EP0-to-Pico callback-handler @ if ep0-handler-to-pico-callback then
      
    ;] debug
  ;
  
  : ep1-handler-to-host ( -- )
    [:

      [ debug? ] [if]
        ." EP1 Handler to Host, Last PID = $" EP1-to-Host next-pid @ h.8 cr
      [then]

      \ Write to clear
      USB_BUFFER_STATUS_EP1_TO_HOST USB_BUFFER_STATUS bis!
      
      EP1-to-Host usb-toggle-data-pid  
      EP1-to-Host callback-handler @ ?execute
      
    ;] debug
  ;

  : ep1-handler-to-pico ( -- )
    [:

      [ debug? ] [if]
        ." EP1 Handler to Pico, Last PID = $" EP1-to-Pico next-pid @ h.8 cr
      [then]

      \ Write to clear
      USB_BUFFER_STATUS_EP1_TO_PICO USB_BUFFER_STATUS bis!
      
      EP1-to-Pico usb-toggle-data-pid  
      EP1-to-Pico callback-handler @ ?execute
      
    ;] debug
  ;

  : ep3-handler-to-host ( -- )
    [:

      [ debug? ] [if]
        ." EP3 Handler to Host, Last PID = $" EP3-to-Host next-pid @ h.8 cr
      [then]

      \ Write to Clear
      USB_BUFFER_STATUS_EP3_TO_HOST USB_BUFFER_STATUS bis!
      
      EP3-to-Host usb-toggle-data-pid 
      
    ;] debug
  ;
  
  : usb-handle-buffer-status ( -- )
    [:

      [ debug? ] [if]
        debug-separator
      [then]

      USB_BUFFER_STATUS @ { buffer-status }

      [ debug? ] [if]
        ." USB Buffer Status Handler = $" buffer-status h.8 cr
      [then]

      buffer-status USB_BUFFER_STATUS_EP0_TO_HOST and if
        ep0-handler-to-host
      then 
      buffer-status USB_BUFFER_STATUS_EP0_TO_PICO and if
        ep0-handler-to-pico
      then 
      buffer-status USB_BUFFER_STATUS_EP1_TO_HOST and if
        ep1-handler-to-host
      then 
      buffer-status USB_BUFFER_STATUS_EP1_TO_PICO and if
        ep1-handler-to-pico
      then 
      buffer-status USB_BUFFER_STATUS_EP3_TO_HOST and if
        ep3-handler-to-host
      then 

    ;] debug
  ;
  
  : usb-irq-handler ( -- )
    [:
      USB_INTS @ { ints }
      
      [ debug? ] [if]
        debug-separator
        
        ." IRQ Handler Active Interrupt(s) = $" ints h.8 cr
      [then]
      
      ints USB_INTS_BUFFER_STATUS and if usb-handle-buffer-status then
      ints USB_INTS_SETUP_REQ and if usb-handle-setup-packet then
      ints USB_INTS_BUS_RESET and if usb-handle-bus-reset then
      
      IRQ_counter @ 1+ IRQ_counter !
      
      [ debug? ] [if]
        ." Interrupts after IRQ handler = $" USB_INTS @ h.8 ." , IRQ Cleared Count = $" IRQ_counter @ h.8 cr
      [then]
    ;] debug
  ;

  : usb-insert-device ( -- )
    [:
     
      [ debug? ] [if]
        cr debug-separator
      
        ." Inserting USB Device ... " cr
      [then]
      
      0 IRQ_counter !
      
      USB_INTS_BUS_RESET          USB_INTE bis!
      USB_INTS_SETUP_REQ          USB_INTE bis!
      USB_INTS_BUFFER_STATUS      USB_INTE bis!
      
      USB_SIE_CTRL_EP0_INT_1BUF   USB_SIE_CONTROL bis! 
      USB_SIE_CTRL_PULLUP_EN      USB_SIE_CONTROL bis!

      [ debug? ] [if]
        ." IRQ Enabled INTS = $"    USB_INTE @ h.8 
        ." , SIE Control = $"       USB_SIE_CONTROL @ h.8 cr
      [then]
      
    ;] debug
  ;

  : usb-remove-device ( -- )
    [:

      [ debug? ] [if]
        ." Removing USB Device ... " cr
      [then]
      
      USB_INTS_BUS_RESET              USB_INTE bic!
      USB_INTS_SETUP_REQ              USB_INTE bic!
      USB_INTS_BUFFER_STATUS          USB_INTE bic!
      
      USB_SIE_CTRL_PULLUP_EN          USB_SIE_CONTROL bic!
      USB_SIE_CTRL_EP0_INT_1BUF       USB_SIE_CONTROL bic! 
      USB_MAIN_CTRL_CONTROLLER_EN     USB_MAIN_CONTROL bic!
      
    ;] debug
  ;

  \ Close USB endpoint
  : close-usb-endpoint { endpoint -- }
    endpoint endpoint-control @ ?dup if 0 swap ! then
    endpoint buffer-control @ ?dup if 0 swap ! then
  ;
  
  \ Close USB endpoints
  : close-usb-endpoints ( -- )
    $1F USB_EP_ABORT !
    begin USB_EP_ABORT_DONE @ $1F and $1F = until
    EP1-to-Host busy? @ not if EP1-to-Host usb-send-zero-length-packet then
    EP1-to-Pico busy? @ not if EP1-to-Pico usb-send-zero-length-packet then
    EP3-to-Host close-usb-endpoint
    EP1-to-Pico close-usb-endpoint
    EP1-to-Host close-usb-endpoint
    EP0-to-Pico close-usb-endpoint
    EP0-to-Host close-usb-endpoint
    usb-remove-device
  ;

  \ Get DTR setting
  : usb-dtr? ( -- dtr? )
    line-state-notification line-value h@ 1 and 0<>
  ;
  
  \ Initialize USB
  : init-usb ( -- )
    [:

      [ debug? ] [if]
        cr debug-separator
        
        ." Initialising USB Core ... " cr
      [then]

      0 usb-device-configured-callback !
      false prepare-usb-device-configured? !
      false usb-device-configured? !
      
      reset-usb-hardware
      
      usb-init-setup-packet
      
      USB_DPRAM_Base dpram-size 0 fill
      
      init-usb-default-endpoints   
      
      $40 usbctrl-irq NVIC_IPR_IP!  
      
      ['] usb-irq-handler usbctrl-vector vector!
      
      USB_USB_MUXING_TO_PHY                 USB_USB_MUXING bis!
      USB_USB_MUXING_SOFTCON                USB_USB_MUXING bis!
      
      USB_USB_PWR_VBUS_DETECT               USB_USB_POWER bis!
      USB_USB_PWR_VBUS_DETECT_OVERRIDE_EN   USB_USB_POWER bis!
      
      USB_MAIN_CTRL_CONTROLLER_EN           USB_MAIN_CONTROL !
      
      usbctrl-irq NVIC_ISER_SETENA!
      
    ;] debug
  ;

end-module

compile-to-ram
