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

begin-module i2c-test

  task import
  task-pool import
  i2c import
  
  2 constant task-count
  task-count task-pool-size buffer: my-task-pool
  
  61 constant recv-buffer-size
  recv-buffer-size buffer: recv-buffer
  
  : init-test ( -- )
    0 [ i2c-internal ] :: init-i2c
    1 [ i2c-internal ] :: init-i2c
    0 master-i2c
    1 slave-i2c
    0 10-bit-i2c-addr
    1 10-bit-i2c-addr
    $55 0 i2c-target-addr!
    $55 1 i2c-slave-addr!
    0 enable-i2c
    1 enable-i2c
    1 14 i2c-pin
    1 15 i2c-pin
    0 16 i2c-pin
    0 17 i2c-pin
    320 128 512 task-count my-task-pool init-task-pool
  ;
  
  : do-test-0 ( -- )
    0 [:
      1 wait-i2c-master-send
      recv-buffer recv-buffer-size 1 i2c>
      recv-buffer swap type
    ;] my-task-pool spawn-from-task-pool run
    pause 1000 ms
    0 [:
      s" FOO" 0 >i2c .
      s" BAR" 0 >i2c .
      s" BAZ" 0 >i2c-stop .
    ;] my-task-pool spawn-from-task-pool run
  ;
  
  : do-test-1 ( -- )
    0 [:
      1 wait-i2c-master-recv
      s" FOO" 1 >i2c .
      s" BAR" 1 >i2c .
      s" BAZ" 1 >i2c .
    ;] my-task-pool spawn-from-task-pool run
    pause 1000 ms
    0 [:
      recv-buffer 9 0 i2c-stop>
      recv-buffer swap type
    ;] my-task-pool spawn-from-task-pool run
  ;

end-module