# Privacy Policy

Last updated: 11 November 2026

This policy covers the Discord bot **Ya like Jazz?** ("the bot"), operated by Pannoniae. It explains what data the bot
receives from Discord, what it does with it, and what it keeps.

Contact: open an issue at https://github.com/Pannoniae/EconomyBot/issues

## The short version

- The bot keeps no database of users or messages.
- The only thing it saves on its own server is per-server configuration: IDs of servers, channels and roles.
- Message content is processed in memory to run the features below and is not sold, shared for advertising, or used
  to train machine learning or AI models.

## What the bot receives and why

### Message content

The bot reads messages in servers it is in for these purposes:

- **Commands.** Messages starting with the bot's prefix are read to run the command. Every command is also
  available as a slash command, which does not involve message content.
- **Ticket transcripts.** When a ticket is closed, the bot reads the messages in that ticket channel once and posts
  them as a text file, together with the ticket's attachments, to the transcript channel the server's administrators
  chose. The transcript stays on Discord, in that server.
- **Deletion log.** If a server's administrators turn it on, when a message is deleted the bot reposts its text
  and attachments to the log channel they chose. This is on Discord, in the same server.
- **Spam and abuse filters.** Messages are checked against a short list of known spam and abuse patterns. Matching
  messages are removed, and in the worst case the poster is banned.

To do this the bot holds recent messages in memory. That cache is small, is never written to disk, and is gone
whenever the bot restarts.

### Server members

- **Join/leave log.** If a server's administrators turn it on, the bot posts a message to a channel of their choice
  when someone joins or leaves, showing the username, user ID, avatar, the server's member count, and the account
  creation date (on joining) or the date they joined (on leaving).
- **Join role.** If a server's administrators turn it on, new members are given a role of their choice.
- **Role commands.** Administrators can count how many members have a role or give a role to all members. The
  member list is fetched for that command and then discarded.

### Voice state

The bot sees which voice channel a member is in so that music commands only work for people in the bot's channel.

## What the bot stores

On the machine the bot runs on:

- **Server configuration:** server ID, the channel IDs chosen for the join/leave log, deletion log and ticket
  transcripts, the ticket category ID, the join role and staff role IDs, and a counter for numbering tickets.

This file is encrypted at rest.

Nothing else. No usernames, messages, attachments or member lists are saved outside Discord. The bot's console
output is not kept. Unexpected errors are reported to a private channel on the operator's own Discord server; those
reports can contain server, channel, message or user IDs, but no message content.

## Third parties

When you ask the bot to find music, the search text you give that command is sent to the music source being
searched. The image commands fetch pictures and articles from public websites (Reddit, an image board, Wikipedia,
xkcd) and send them nothing you typed, apart from a comic number. In both cases no Discord IDs, usernames or other
messages go with the request.

Data is not shared with anyone else.

## Retention and deletion

- Server configuration is kept until an administrator changes it or it is removed on request.
- Transcripts, join/leave log messages and deletion log messages are ordinary Discord messages in the server they belong
  to. The administrators of that server control them and can delete them at any time.

To have a server's configuration removed, or to ask what the bot holds about you, use the contact above.

## Opting out

Message processing for moderation (the deletion log, spam filters and ticket transcripts) cannot be turned off per
user, because a moderation record that people can opt out of does not work. If you do not want the bot to process
your messages, do not use servers it is in, or ask that server's administrators to remove it.

## Changes

Changes to this policy are published in this file, with the date at the top updated.
