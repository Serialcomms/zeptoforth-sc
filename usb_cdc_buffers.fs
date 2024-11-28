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

marker remove-usb-cdc-buffers

  begin-module usb-cdc-buffers

    \ TX pending operation
    pending-op-size buffer: tx-pending-op

    \ Debug console only ( will try to toggle with break )
    variable console-debug?

    \ Ready to send more data
    variable next-tx-initial?

    \ Saved reboot hook
    variable saved-reboot-hook
    
    \ Are special keys enabled for USB
    variable usb-special-enabled

    \ RAM variable for rx buffer read-index
    variable rx-read-index

    \ RAM variable for tx buffer read-index
    variable tx-read-index
    
    \ RAM variable for rx buffer write-index
    variable rx-write-index
    
    \ RAM variable for tx buffer write-index
    variable tx-write-index

    \ Constant for number of bytes to buffer
    1024 constant rx-buffer-size
    
    \ Constant for number of bytes to buffer
    1024 constant tx-buffer-size

    \ RX buffer to Pico
    rx-buffer-size buffer: rx-buffer
    
    \ TX buffer to Host
    tx-buffer-size buffer: tx-buffer

     \ Constant for tx buffer size mask 
    tx-buffer-size 1- constant tx-buffer-size-mask

    \ Constant for rx buffer size mask 
    rx-buffer-size 1- constant rx-buffer-size-mask 

    \ TX buffer that is not circular - todo - deprecate
    tx-buffer-size buffer: tx-straight-buffer

    \ The TX core lock
    core-lock-size buffer: tx-core-lock

    \ The RX core lock
    core-lock-size buffer: rx-core-lock

    : init-rx ( -- )
      0 rx-read-index !
      0 rx-write-index !
      rx-buffer rx-buffer-size 0 fill
    ;

    : init-tx ( -- )
      0 tx-read-index !
      0 tx-write-index !
      tx-buffer tx-buffer-size 0 fill
    ;
    
    \ Get whether the rx buffer is full
    : rx-full? ( -- f )
      rx-read-index @ 1- $FF and rx-write-index @ =
    ;

    \ Get whether the rx buffer is empty
    : rx-empty? ( -- f )
      rx-read-index @ rx-write-index @ =
    ;

    \ Get number of bytes available to read from the rx buffer
    : rx-count ( -- u )
      rx-read-index @ { read-index }
      rx-write-index @ { write-index }
      read-index write-index <= if
        write-index read-index -
      else
        rx-buffer-size read-index - write-index +
      then
    ;

    : rx-used ( -- u) \ synonym for rx-count above 
     rx-read-index @ { read-index }
     rx-write-index @ { write-index }
     read-index write-index <= if
        write-index read-index -
     else
      rx-buffer-size read-index - write-index +
     then
    ;

    \ Get number of free bytes available in rx buffer
    : rx-free ( -- bytes )
      rx-buffer-size 1- rx-used -
    ;

    \ Write a byte to the rx buffer
    : write-rx ( c -- )
      rx-full? not if
        rx-write-index @ rx-buffer + c!
        rx-write-index @ 1+ rx-buffer-size-mask and rx-write-index !
      else
        drop
      then
    ;

    \ Read a byte from the rx buffer
    : read-rx ( -- c )
      rx-empty? not if
        rx-read-index @ rx-buffer + c@
        rx-read-index @ 1+ rx-buffer-size-mask and rx-read-index !
      else
        0
      then
    ;

    \ Get whether the tx buffer is full
    : tx-full? ( -- f )
      tx-read-index @ 1- tx-buffer-size-mask and tx-write-index @ =
    ;

    \ Get whether the tx buffer is empty
    : tx-empty? ( -- f )
      tx-read-index @ tx-write-index @ =
    ;

    \ Get number of bytes available in the tx buffer
    : tx-count ( -- u )
      tx-read-index @ { read-index }
      tx-write-index @ { write-index }
      read-index write-index <= if
        write-index read-index -
      else
        tx-buffer-size read-index - write-index +
      then
    ;

    : tx-used ( -- u) \ synonym for tx-count above 
      tx-read-index @ { read-index }
      tx-write-index @ { write-index }
      read-index write-index <= if
        write-index read-index -
      else
        tx-buffer-size read-index - write-index +
      then
    
    ;

   \ Get number of free bytes available in tx buffer
    : tx-free ( -- bytes )
      tx-buffer-size 1- tx-used -
    ;

    \ Write a byte to the tx buffer
    : write-tx ( c -- )
      tx-full? not if
        tx-write-index @ tx-buffer + c!
        tx-write-index @ 1+ tx-buffer-size-mask and tx-write-index !
      else
        drop
      then
    ;

    \ Read a byte from the tx buffer
    : read-tx ( -- c )
      tx-empty? not if
        tx-read-index @ tx-buffer + c@
        tx-read-index @ 1+ tx-buffer-size-mask and tx-read-index !
      else
        0
      then
    ;
