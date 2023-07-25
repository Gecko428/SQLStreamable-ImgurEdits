# SQLStreamable-ImgurEdits
A modification of Kyraryc's code to pull videos from computer and upload to imgur

**To get files from a specific folder**

* set the boolean on line 722 to true.

* Specify a video folder in line 715 where your gifs/videos are downloaded to. It will find videos in subfolders so don't worry about that.

* Make sure the download folder is **different** than the video folder if you don't want the videos deleted

**To upload to imgur**, 

* set upload imgur at line 724 to true

* Add your imgur account email and password to lines 719 and 720

* Wait for the actual upload window to appear. Actual uploads, verifying you want sound, and "Are you human" checks are done by hand

* If you hit "CREATE_ALBUM_FAIL", wait about an hour and then press any key on the command line to continue the program.
