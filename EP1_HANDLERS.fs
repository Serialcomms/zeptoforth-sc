: ep1-start-queue-runner-to-host ( - - )

  tx-count 64 min { tx-bytes }
   
  tx-bytes if

  tx-bytes 0 do

   read-tx EP1-to-Host dpram-address @ i + c! 

  loop

   EP1-to-Host tx-bytes usb-send-data-packet

  then
;

: ep1-handler-to-host ( -- )

  USB_BUFFER_STATUS_EP1_TO_HOST USB_BUFFER_STATUS bis!     \ Write to Clear

  EP1-to-Host usb-toggle-data-pid 

  tx-count if 

    ep1-start-queue-runner-to-host

  else

    false EP1-to-Host busy? !

  then

;

: ep1-handler-to-pico ( -- )

  USB_BUFFER_STATUS_EP1_TO_PICO USB_BUFFER_STATUS bis!     \ Write to Clear

  EP1-to-Pico usb-toggle-data-pid 

  EP1-to-Pico buffer-control @ @ USB_BUF_CTRL_LEN_MASK and { received-bytes }

  received-bytes if

  received-bytes 0 do

    rx-full? not if

      EP1-to-Pico dpram-address @ i + c@ write-rx

    then

  loop

  then

  rx-free 63 > if

    false EP1-to-Pico queue-long? !

    EP1-to-Pico 64 usb-receive-data-packet  
    \ start receive packet - we now have rx capacity for it

  else

    true EP1-to-Pico queue-long? !

  then
;
