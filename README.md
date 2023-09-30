# Virtual Monitor Helper
A little system tray utility which ensures that Sunshine's configuration file is kept up to date with IddSampleDriver's display name and gracefully handles display configuration changes.

When launched, it will determine the display ID currently associated with IddSampleDriver, write that value to the output_device field of Sunshine's configuration file
and then restart the Sunshine service.  It will then listen continuously for Windows changes to Windows display configuration (resolution changes, connections, disconnects and so on).  
When such a change is detected, it will check to see whether IddSampleDriver's display ID has changed.  If the display ID has changed, then it will update Sunshine's configuration file and restart the
Sunshine service.

If the IddSampleDriver device crashes, is disabled or otherwise lost, the helper will wait until it returns and then perform its tasks.


