: usb-emit { transmit-character }               \ Emit a byte towards the host

      begin tx-full? if while pause repeat then     \ wait (block) for queue capacity to host
      
        ep1-to-host busy? @ if

            transmit-character write-tx             \ add byte to already-running queue ( not-full queue has at least 1 byte capacity )

            ep1-to-host busy? @ not if              \ EP may have finished by the (short) time it takes to get here

            ep1-start-queue-runner-to-host          \ 

            then
        
        else

            ep1-to-host 1 ' transmit-character usb-build-data-packet    \ skip queue as not already running

            ep1-to-host 1 send-data-packet                              \ send a 1-byte packet to host
      
        then

    ;

   : usb-key ( -- c)

    rx-empty? if

        false                                       \ no data waiting - return false

    else

        read-rx { receive-character }               \ save character for now

        ep1-to-pico queue-long? @ if                \ is queue previously marked as long ( < 64 free bytes remaining ) ?

        rx-count 63 > if                            \ did read-rx make 64 bytes or more available to receive ?

            false ep1-to-pico queue-long? !         \ cancel queue-long ( if previously set by ep1-handler-to-pico )

            ep1-to-pico 64 usb-receive-data-packet  \ start another receive packet which we now have rx capacity for
   
        then

        receive-character

    then

   ;
