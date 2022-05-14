\ Copyright (c) 2021-2022 Travis Bemann
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

continue-module forth

  interrupt import
  gpio import
  pio import
  task import
  systick import

  \ The initial setup
  create pio-init
  1 SET_PINDIRS set,
  0 SET_PINS set,
  
  \ The PIO code
  create pio-code
  PULL_BLOCK PULL_NOT_EMPTY pull,
  32 OUT_X out,
  1 SET_PINS set,
  3 COND_X1- jmp,
  PULL_BLOCK PULL_NOT_EMPTY pull,
  32 OUT_X out,
  0 SET_PINS set,
  7 COND_X1- jmp,

  \ The blinker state
  variable blinker-state

  \ The blinker maximum input shade
  variable blinker-max-input-shade

  \ The blinker maximum shade
  variable blinker-max-shade
  
  \ The blinker shading
  variable blinker-shade

  \ The blinker shade step delay in 100 us increments
  variable blinker-step-delay

  \ The blinker shade conversion routine
  : convert-shade ( i -- shade ) 0 swap 2dup 2dup f* 0,01 f* d+ nip ;

  \ PIO interrupt handler
  : handle-pio ( -- )
    blinker-state @ not if
      blinker-shade @ 0 PIO0 TXF !
    else
      blinker-max-shade @ blinker-shade @ - 0 PIO0 TXF !
    then
    blinker-state @ not blinker-state !
    0 INT_SM_TXNFULL PIO0 INTR !
    PIO0_IRQ0 NVIC_ICPR_CLRPEND!
  ;

  \ Set the sahding
  : blinker-shade! ( i -- )
    blinker-max-input-shade @ convert-shade blinker-max-shade !
    convert-shade blinker-shade !
  ;
  
  \ The blinker shading task loop
  : blinker-shade-loop ( -- )
    begin
      blinker-max-input-shade @ 0 ?do
	i blinker-shade!
	blinker-step-delay @ systick-counter current-task delay
      loop
      0 blinker-max-input-shade @ ?do	
	i blinker-shade!
	blinker-step-delay @ systick-counter current-task delay
      -1 +loop
    again
  ;

  \ Init blinker
  : init-blinker ( -- )
    true blinker-state !
    500 blinker-max-input-shade !
    25 blinker-step-delay !
    0 blinker-shade!
    %0000 PIO0 CTRL_SM_ENABLE!
    %0001 PIO0 CTRL_SM_RESTART!
    6 25 GPIO_CTRL_FUNCSEL!
    25 bit GPIO_OE_SET !
    25 bit GPIO_OUT_CLR !
    781 0 PIO0 SM_CLKDIV_INT!
    0 0 PIO0 SM_CLKDIV_FRAC!
    1 0 PIO0 SM_PINCTRL_SET_COUNT!
    25 0 PIO0 SM_PINCTRL_SET_BASE!
    0 0 PIO0 SM_EXECCTRL_WRAP_BOTTOM!
    7 0 PIO0 SM_EXECCTRL_WRAP_TOP!
    true 0 PIO0 SM_EXECCTRL_OUT_STICKY!
    pio-init 2 0 PIO0 sm-instr!
    pio-code 8 PIO0 instr-mem!
    0 0 PIO0 SM_ADDR !
    blinker-shade @ 0 PIO0 TXF !
    ['] handle-pio PIO0_IRQ0 16 + vector!
    0 INT_SM_TXNFULL IRQ0 PIO0 INTE bis!
    PIO0_IRQ0 NVIC_ISER_SETENA!
    %0001 PIO0 CTRL_SM_ENABLE!
    0 ['] blinker-shade-loop 420 128 512 spawn run
  ;

end-module
