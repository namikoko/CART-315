# Prototype 02 – Snake Bird

For this prototype we were given three template games (Flappy Bird, Brick Breaker and Snake) and asked to mod one of them, or mix a few together. I started with Flappy Bird and borrowed the main idea from Snake.

## The idea

In Snake Bird, little treats float between the pipes. Each time you eat one, a baby chick joins a line behind you, like a snake's tail. A longer tail gives you a bigger score multiplier, but if any chick bumps into a pipe, the run ends. So growing is both the reward and the risk, and the game gets harder the better you're doing.

## Look and feel

For inspiration I used cute pastel mobile games and soft kawaii interfaces, with lots of pink, gingham, polka dots, clouds and little hearts. I redid all the art in that style: a pink bird with a bow, pipes that look like frosted candy sticks, a lace and gingham ground, and treats like strawberries, macarons and star cookies. I also added a gentle music-box song and small sounds for flapping, eating and the chicks joining in.

## Process

Most of the work was building on what the template already had instead of starting over. The tail follows the bird with a bit of delay, so big flaps make it swing, and that's what makes the tail tricky to manage. I also moved the game over screen to the centre, added a best score, and made the music stop when you lose.

A few things went wrong along the way. At one point the game didn't stop when you lost, because the scripts had reloaded while the game was still running. Restarting play mode fixed it. Small adjustments like the music volume took a few rounds of playing and tweaking.

## Reflection

I liked how one small change, adding a tail, changed the whole feel of Flappy Bird. It went from just dodging to making choices about when to go for a treat. If I kept going, I would play with how floppy the tail is, and maybe add a few special treats with different effects.
