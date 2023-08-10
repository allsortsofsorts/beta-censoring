# How is this different from the normal backend?

This is basically the same as the original server but it can optionally connect to a Discord bot. If the server doesn't find a token.txt, it will print a message like Failed to load Discord token from ..., but otherwise start normally.
You should still send an have the user load their .betaoverride as normal, as You would want the settings locked should they use the normal backend. 

# Bot Usage

Discord's policy requires that You be in a common server with anyone You DM, You'll need to join the server of the bot You want to message. Simply DM the bot anything to get it started. Feel free to mute the server after joining, You'll never need to do anything there.

## Commands

### !force-config

The easiest way to get a valid overide is to export the .betaoverride file from the browser addon. Paste it after !force-config an example would be:
```
!force-config {"key":"...","id":"...","allowedModes":["enabled"],"preferences":{"covered":{"Pits":{"method":"none","level":5}}},"obfuscateImages":false,"errorMode":"subtle","videoCensorMode":"Blur","videoCensorLevel":3,"allowList":[],"forceList":[],"autoAnimate":false},"hash":-1974218759}
```

The backend only does anything with the parts inside of "preferences", as the rest of the config is not used by the server, but it's easier to paste that instead of formatting it correctly by hand.

Once You have a valid config You can feel free to hand edit the different methods, or generate a new config to paste. 

The different methods of censoring are "none", "caption", "blackbox", "pixel", "sticker", "blur" should You choose to hand edit them.

The addon only lets You choose levels 1-20, but there is no limit with this. A level of 300 on any censor will typically apply it to the entire image.

If Your command works the bot should respond by saying: Updated Preferences

If Your command fails the bot should respond by saying: Error Updating Preferences.

The bot will respond to both new messages and edits.

Note: If You pin a message starting with !force-config, then anytime that the server is started it will first read the pinned messages and apply the !force-config as if it had just been sent.