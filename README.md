WebCam
======

A simple application designed to catch the person who turns off my air conditioner. You place a webcam and mark some areas as "hotspots". If anything is moving in those hotspots the camera records for a few seconds before and after the move.
The camera records all the time to a cyclic buffer. Once it detects that it needs to save the file it saves the buffer, fills the buffer again and saves it again. This way the recording is several seconds before and after the event.

#Notice
The code is very bad! Don't use it as an example for coding style, OOP, or anything else of the sort.
I wrote it in 2 hours just to catch the person in question.