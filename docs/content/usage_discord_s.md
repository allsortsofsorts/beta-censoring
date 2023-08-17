# Setup a Discord Bot

In order to use the additional discord functionality you will need a Discord Token. You can use your own user token, but that is against Discord TOS, so this will describe a proper bot setup instead.

## Setup a Server for the bot
1. Go to https://discord.com/channels/@me
1. Click the + in the left menu
1. Click Create My Own
1. Pick anything here
1. Name your server something with your username in it.

## Setup the bot
1. Go to https://discord.com/developers/applications
1. Click New Application in the top right corner - Name it something similiar to your username but append -bot to it.
1. Click Bot in the left menu
1. Toggle on Message Content Intent on the right menu.
1. Click OAuth2 in the left Menu
1. Click URL Generator in the left Menu
1. Click bot under the scopes in the right side of the page
1. Click Send Messages and Read Message History under bot permissions >> text permissions 
1. Copy the Generated URL at the bottom of the page and open it
1. Select the Server you created earlier and add the bot to it.

## Consume the bot token
1. Go to https://discord.com/developers/applications
1. Click Bot in the left menu
1. Uncheck Public Bot
1. Click Reset Token, copy this token down. DO NOT SHARE IT WITH ANYONE.
1. In the same folder that you have BetaCensor.Server.exe make a new file named token.txt, paste the token there.
1. Start the server, if everything is correct you should see your bot is online.
1. You can confirm the bot is working by messaging "!test" if its good the bot will respond.

## Invite whoever you want to talk to the bot to your server
1. There's only one user that the bot responds to, so i wonder who you would invite?