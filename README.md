# LuteBot 3
Originally forked from https://github.com/MontyLeGueux/Lutebot-2

![Track Filtering Example](https://github.com/Dimencia/LuteBot3/blob/master/LutebotExample1.PNG)

![Guild Library Example](https://github.com/Dimencia/LuteBot3/blob/master/LutebotExample2.PNG)

## New Features v Lutebot-2
* Searching/filtering/downloading of any song in the Bards Guild Midi Library - Bard's Guild: https://discord.gg/4xnJVuz
* Automatic search/download of BitMidi when searching for a song not in the Library
* Ability to visually align songs or individual channels to match your instrument
* Song preferences are now injected into the midi file, so the mid can be distributed easily with settings intact
* Automatic drum removal for Mordhau
* TimeSync option using an NTP server to attempt to synchronize playing with a friend
* QOL tweaks, such as a stop hotkey, not unloading song on stop, song reload button


### Guild Library
You can find this in Window -> Open -> Guild Library

Currently, you will need to have the Guild Library downloaded and extracted to %appdata%/LuteBot/GuildLibrary/songs

These folders will be created for you when you run LuteBot

The Guild Library can be downloaded from the [Bards Guild Discord](https://discord.gg/MmWbkJK)

Very soon, these songs will support auto-download from the Library

If you do not have the library, it will still attempt to auto-download the songs from the internet, but many songs will fail to be found (and another similar song from bitmidi, usually bad, will be downloaded)

### Track Alignment
New feature lets you align channels individually to the instrument range, as shown in the image

This helps you accentuate or soften parts of the song based on where you put them on the instrument range

These changes are all saved with the midi

### Midi Embed
Starting with v2.41, Track Filtering selections and track alignment data are all stored inside the .mid file itself

Previously it was stored in an xml file, and when distributing mids, you had to either send two files or tell them how to filter it

Now, once you find the perfect settings for a mid, you can send that adjusted mid to someone and have them load it directly

All mids with embedded data are still compatible with any other midi players
