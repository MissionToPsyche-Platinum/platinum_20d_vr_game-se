# Event Mode test guide (build 0.9.0)

This build is the Event Mode kiosk: the version a visitor plays at a public event. It starts
straight in the Psyche mission room. A visitor has 2.5 minutes to build the spacecraft,
then the launch video plays. Please play it as a first-time visitor would, then tell us
what was hard, confusing, uncomfortable or broken.

## Before you start

1. Install `PsycheVR-Event.apk` with SideQuest or `adb install -r PsycheVR-Event.apk`. It
   appears under Library > Unknown Sources as "Psyche VR Experience".
2. Set a room-scale boundary about 2 x 2 m around where the player stands. The game puts
   every player on the same start spot, facing the screen, at every start and every reset.
3. Keep the headset on Wi-Fi. Session logs upload by themselves; there is nothing to send.

## Controls

The next version adds a short controls screen at the start for each player. Until then,
tell players this before they put the headset on:

| To do this | Do this |
| --- | --- |
| Pick something up | Reach out and hold the grip button or the trigger (either one works). Let go to drop or throw. |
| Reach something on the floor | Point an empty hand at it. It floats up to you. |
| Press a key, button or the ping dome | Tap or slap it with your hand. |
| Click a pen | Hold the pen, then press the other button on the same controller (trigger if you grabbed with the grip). |
| Crumple a paper sheet | Hold the sheet, then squeeze the other button on the same controller. Then throw it. |
| Read a book | Pick it up and it opens. Turn pages with your other hand. |
| Open a drawer | Grab the handle and pull. Push it to close. |
| Move around | Take a step. There is no teleport in Event Mode; everything is within reach. |

The controller menu button does nothing on purpose, so players cannot open a menu by
accident.

## Staff reset (between players)

Hold all six of these at the same time for 4 seconds:

- Left controller: grip + trigger + Y
- Right controller: grip + trigger + press the thumbstick down

A ring appears in the middle of the view after 1 second and fills up. When it is full, the
room reloads for the next player: every object goes back, the clock re-arms, and the player
is placed back on the start spot. Letting go early cancels it.

## What happens in a session

1. The clock starts at the player's first grip or trigger press, so handing over the
   headset does not count.
2. The goal is the spacecraft puzzle: place the four pieces (body, antenna and the two solar
   arrays) onto the model on the puzzle table. The board beside it has Next and Back for
   the instructions, and Reset to send loose pieces back.
3. When the puzzle is finished, or after 2.5 minutes, the launch video plays on the wall
   screen and a message appears. The player can keep exploring afterwards.
4. Do the staff reset before the next player.

## What to try

- [ ] Finish the puzzle without help. Did the instructions make sense?
- [ ] Let the clock run out instead. Did the video start, and did the ending feel clear?
- [ ] Mission console: slap the orange PING dome to send a radio signal to Psyche and watch
      the Deep Space Network screen for the reply. Use Esc, Enter and Space to change
      screens, and the power button to turn it off and on.
- [ ] Pick up the mission headset on the ledge and bring it to your ear.
- [ ] Pens, paper (crumple a sheet and throw it in the basket), basketballs, drawers, the
      coffee mug, the plant and the book "Journey to a Metal World" on the bookshelf.
- [ ] Drop something on the floor and get it back by pointing at it.
- [ ] Do the staff reset in the middle of a session, during the video, and after it.
- [ ] Anything that felt uncomfortable: motion, text too small to read, objects out of reach.

## Still placeholder in this build

- Sounds are a first pass and may change.
- No controls screen yet (coming in the next version, see above).
- Text and images on the console screens are being checked against NASA and ASU sources.

## Reporting

For each problem, write what you did, what you expected and what happened. Include the
time of day, so we can find your session in the logs.
