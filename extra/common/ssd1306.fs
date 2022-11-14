\ Copyright (c) 2022 Travis Bemann
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

begin-module ssd1306

  oo import
  i2c import
  bitmap import
  bitmap-internal import

  \ An I2C SSD1306 device
  <bitmap> begin-class <ssd1306>
    
    begin-module ssd1306-internal

      \ I2C device
      cell member ssd1306-device

      \ I2C address
      cell member ssd1306-addr

      \ I2C pin 0 (SCK or SDA, does not matter)
      cell member ssd1306-pin0

      \ I2C pin 1 (SCK or SDA, does not matter)
      cell member ssd1306-pin1
      
      \ Command base register
      cell member ssd1306-cmd-base

      \ Dirty rectangle start column
      cell member ssd1306-dirty-start-col

      \ Dirty rectangle end column
      cell member ssd1306-dirty-end-col

      \ Dirty rectangle start row
      cell member ssd1306-dirty-start-row

      \ Dirty rectangle end row
      cell member ssd1306-dirty-end-row
      
    end-module> import

    \ Update the SSD1306 device
    method update
    
  end-class

  \ Default I2C address
  $3C constant SSD1306_I2C_ADDRESS

  continue-module ssd1306-internal
    
    \ SSD1306 Commands - see Datasheet

    \ Indicates following bytes are commands
    $00 constant SSD1306_CMD_START

    \ Indicates following bytes are data
    $40 constant SSD1306_DATA_START

    \ Fundamental Command Table (p. 28)

    \ Double-byte command to set contrast (1-256)
    $81 constant SSD1306_SETCONTRAST

    \ Set entire display on
    $A5 constant SSD1306_ENTIREDISPLAY_ON

    \ Use RAM contents for display
    $A4 constant SSD1306_ENTIREDISPLAY_OFF

    \ Invert RAM contents to display
    $A7 constant SSD1306_SETINVERT_ON

    \ Normal display
    $A6 constant SSD1306_SETINVERT_OFF

    \ Display OFF (sleep mode)
    $AE constant SSD1306_SETDISPLAY_OFF

    \ Display ON (normal mode)
    $AF constant SSD1306_SETDISPLAY_ON

    \ Scrolling Command Table (pp. 28-30)

    \ Configure right horizontal scroll
    $26 constant SSD1306_SCROLL_SETUP_H_RIGHT

    \ Configure left horizontal scroll
    $27 constant SSD1306_SCROLL_SETUP_H_LEFT

    \ Configure right & vertical scroll
    $29 constant SSD1306_SCROLL_SETUP_HV_RIGHT

    \ Configure left & vertical scroll
    $2A constant SSD1306_SCROLL_SETUP_HV_LEFT

    \ Configure vertical scroll area
    $A3 constant SSD1306_SCROLL_SETUP_V

    \ Stop scrolling
    $2E constant SSD1306_SCROLL_DEACTIVATE

    \ Addressing Setting Command Table (pp. 30-31)

    \ Start scrolling
    $2F constant SSD1306_SCROLL_ACTIVATE

    \ Set lower 4 bits of column start address by ORing 4 LSBs
    $00 constant SSD1306_PAGE_COLSTART_LOW

    \ Set upper 4 bits of column start address by ORing 4 LSBs
    $10 constant SSD1306_PAGE_COLSTART_HIGH

    \ Set page start address by ORing 4 LSBs
    $B0 constant SSD1306_PAGE_PAGESTART

    \ Set addressing mode (horizontal, vertical, or page)
    $20 constant SSD1306_SETADDRESSMODE

    \ Send 2 more bytes to set start and end columns for hor/vert modes
    $21 constant SSD1306_SETCOLRANGE

    \ Send 2 more bytes to set start and end pages
    $22 constant SSD1306_SETPAGERANGE

    \ Hardware Configuration Commands (p. 31)

    \ Set RAM display start line by ORing 6 LSBs
    $40 constant SSD1306_SETSTARTLINE

    \ Set column address 0 to display column 0
    $A0 constant SSD1306_COLSCAN_ASCENDING

    \ Set column address 127 to display column 127
    $A1 constant SSD1306_COLSCAN_DESCENDING

    \ Set size of multiplexer based on display height (31 for 32 rows)
    $A8 constant SSD1306_SETMULTIPLEX

    \ Set COM 0 to display row 0
    $C0 constant SSD1306_COMSCAN_ASCENDING

    \ Set COM N-1 to display row 0
    $C8 constant SSD1306_COMSCAN_DESCENDING

    \ Set display vertical shift
    $D3 constant SSD1306_VERTICALOFFSET

    \ Set COM pin hardware configuration
    $DA constant SSD1306_SETCOMPINS

    \ Timing and Driving Scheme Settings Commands (p. 32)

    \ Set display clock divide ratio and frequency
    $D5 constant SSD1306_SETDISPLAYCLOCKDIV

    \ Set pre-charge period
    $D9 constant SSD1306_SETPRECHARGE

    \ Set V_COMH voltage level
    $DB constant SSD1306_SETVCOMLEVEL

    \ No operation
    $E3 constant SSD1306_NOP

    \ Charge Pump Commands (p. 62)

    \ Enable / disable charge pump
    $8D constant SSD1306_SETCHARGEPUMP
    
    \ Initialize I2C for an SSD1306 device
    : init-i2c ( ssd1306 -- )
      dup ssd1306-device @
      2dup swap ssd1306-pin0 @ i2c-pin
      2dup swap ssd1306-pin1 @ i2c-pin
      dup master-i2c
      dup 7-bit-i2c-addr
      swap ssd1306-addr @ over i2c-target-addr!
      enable-i2c
    ;

    \ Begin constructing a command to send to the SSD1306
    : begin-cmd ( ssd1306 -- )
      ram-here swap ssd1306-cmd-base !
    ;
    
    \ Write a byte to the current command being constructed
    : >cmd ( c -- ) cram, ;
    
    \ Send the current command that has been constructed
    : send-cmd ( ssd1306 -- )
      [:
        dup ssd1306-cmd-base @ dup ram-here swap - 2 pick
        ssd1306-device @ >i2c-stop
      ;] try ssd1306-cmd-base @ ram-here! ?raise
    ;

    \ Initialize an SSD1306 display
    : init-display ( ssd1306 -- )
      dup begin-cmd
      \ Begin a command
      SSD1306_CMD_START >cmd
      \ Turn off display
      SSD1306_SETDISPLAY_OFF >cmd
      \ Set the clock to Fosc = 8, divide ratio = 1
      SSD1306_SETDISPLAYCLOCKDIV >cmd $80 >cmd
      \ Set the display multiplexer to the number of rows - 1
      SSD1306_SETMULTIPLEX >cmd dup bitmap-rows @ 1 - >cmd
      \ Set the vertical offset to 0
      SSD1306_VERTICALOFFSET >cmd 0 >cmd
      \ RAM start line 0
      SSD1306_SETSTARTLINE $00 or >cmd
      \ Set the charge pump on
      SSD1306_SETCHARGEPUMP >cmd $14 >cmd
      \ Set the addressing mode to horizontal mode
      SSD1306_SETADDRESSMODE >cmd $00 >cmd
      \ Set flip columns
      SSD1306_COLSCAN_DESCENDING >cmd
      \ Set to not flip pages
      SSD1306_COMSCAN_ASCENDING >cmd
      \ Set COM pins to sequential pin mode
      SSD1306_SETCOMPINS >cmd $02 >cmd
      \ Set contrast to minimal
      SSD1306_SETCONTRAST >cmd $00 >cmd
      \ Set precharge period to phase1 = 15, phase2 = 1
      SSD1306_SETPRECHARGE >cmd $F1 >cmd
      \ Set VCOMH deselect level to (0, 2, 3)
      SSD1306_SETVCOMLEVEL >cmd $40 >cmd
      \ Set u se RAM contents for display
      SSD1306_ENTIREDISPLAY_OFF >cmd
      \ Set no inversion
      SSD1306_SETINVERT_OFF >cmd
      \ Set no scrolling
      SSD1306_SCROLL_DEACTIVATE >cmd
      \ Turn on display in normal mode
      SSD1306_SETDISPLAY_ON >cmd
      send-cmd
    ;

    \ Send a row of data to the SSD1305 device
    : send-row ( start-col end-col page ssd1306 -- )
      >r r@ begin-cmd
      r@ page-addr -rot swap ?do dup i + c@ >cmd loop drop
      r> send-cmd
    ;
    
    \ Send an area of data to the SSD1306 device
    : send-area ( start-col end-col start-page end-page ssd1306 -- )
      -rot swap do 3dup i swap send-row loop 2drop drop
    ;
    
    \ Update a rectangular space on the SSD1306 device
    : update-area ( start-col end-col start-row end-row ssd1306 -- )
      >r r@ begin-cmd
      8 align 3 rshift swap 3 rshift swap
      SSD1306_CMD_START >cmd
      SSD1306_SETPAGERANGE >cmd 2dup swap >cmd 1- >cmd
      SSD1306_SETCOLRANGE >cmd 2over swap >cmd 1- >cmd    
      r@ send-cmd
      r> send-area
    ;

    \ Dirty a pixel on an SSD1306 device
    : dirty-pixel ( col row ssd1306 -- )
      >r r@ dirty? if
        dup r@ ssd1306-dirty-start-row @ min r@ ssd1306-dirty-start-row !
        1+ r@ ssd1306-dirty-end-row @ max r@ ssd1306-dirty-end-row !
        dup r@ ssd1306-dirty-start-col @ min r@ ssd1306-dirty-end-col !
        1+ r@ ssd1306-dirty-end-col @ max r> ssd1306-dirty-end-col !
      else
        dup r@ ssd1306-dirty-start-row !
        1+ r@ ssd1306-dirty-end-row !
        dup r@ ssd1306-dirty-start-col !
        1+ r> ssd1306-dirty-end-col !
      then
    ;

    \ Dirty an area on an SSD1306 device
    : dirty-area ( start-col end-col start-row end-row ssd1306 -- )
      4 pick 4 pick < 3 pick 3 pick < and if
        >r swap 2swap -rot swap ( end-row end-col start-col start-row )
        r@ dirty-pixel ( end-row end-col )
        1- swap 1- r> dirty-pixel ( )
      else
        drop 2drop 2drop
      then
    ;
    
  end-module

  <ssd1306> begin-implement
  
    \ Initialize an SSD1306 device
    :noname ( pin0 pin1 frame-buf cols rows i2c-addr i2c-device ssd1306 -- )
      -rot 2>r >r r@ [ <bitmap> ] -> new r>
      tuck ssd1306-device !
      tuck ssd1306-addr !
      tuck ssd1306-pin1 !
      tuck ssd1306-pin0 !
      dup init-i2c
      init-display
    ; define new

    \ Set the entire display to be dirty
    :noname ( ssd1306 -- )
      0 over ssd1306-dirty-start-col !
      dup bitmap-cols @ over ssd1306-dirty-end-col !
      0 over ssd1306-dirty-start-row !
      dup bitmap-rows @ swap ssd1306-dirty-end-row !
    ; define set-dirty

    \ Clear dirty rectangle
    :noname ( ssd1306 -- )
      0 over ssd1306-dirty-start-col !
      0 over ssd1306-dirty-end-col !
      0 over ssd1306-dirty-start-row !
      0 swap ssd1306-dirty-end-row !
    ; define clear-dirty
    
    \ Get whether an SSD1306 device is dirty
    :noname ( ssd1306 -- dirty? )
      >r r@ ssd1306-dirty-start-col @ r@ ssd1306-dirty-end-col @ <>
      r@ ssd1306-dirty-start-row @ r> ssd1306-dirty-end-row @ <> and
    ; define dirty?
  
    \ Update the SSD1306 device
    :noname ( ssd1306 -- )
      >r r@ dirty? if
        r@ ssd1306-dirty-start-col @
        r@ ssd1306-dirty-end-col @
        r@ ssd1306-dirty-start-row @
        r@ ssd1306-dirty-end-row @
        r@ update-area
        r> clear-dirty
      else
        rdrop
      then
    ; define update

  end-implement
  
end-module