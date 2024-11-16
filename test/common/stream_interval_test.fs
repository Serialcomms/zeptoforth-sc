\ Copyright (c) 2024 Travis Bemann
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

begin-module stream-interval-test

  task import
  stream import

  1024 constant stream-bytes
  stream-bytes stream-size buffer: my-stream

  : run-test ( -- )
    stream-bytes my-stream init-stream
    0 [:
      begin
        s" Foobar! " my-stream send-stream
      again
    ;] 320 128 512 spawn { send-task }
    1 send-task task-interval!
    0 [:
      16 [: { buffer }
        begin
          buffer 16 my-stream recv-stream buffer swap type
        again
      ;] with-allot
    ;] 320 128 512 spawn { recv-task }
    1 recv-task task-interval!
    send-task recv-task [: run run ;] critical
  ;
  
end-module
