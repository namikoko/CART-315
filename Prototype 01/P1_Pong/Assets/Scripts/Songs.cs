using System;

// A song is just a title and a list of notes, written like "C4 D4 E4 F#4 Bb3".
// Letter + optional # (sharp) or b (flat) + octave number. Middle C is C4.
// Each paddle hit plays the next note, so rhythm comes from the rally, not the list.
public class Song
{
    public string title;
    public string[] notes;

    public Song(string title, string notes)
    {
        this.title = title;
        this.notes = notes.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    }
}

public static class Songs
{
    // To add a song, add another "new Song(...)" line to this list.
    public static readonly Song[] All =
    {
        new Song("Ode to Joy", @"
            E4 E4 F4 G4 G4 F4 E4 D4 C4 C4 D4 E4 E4 D4 D4
            E4 E4 F4 G4 G4 F4 E4 D4 C4 C4 D4 E4 D4 C4 C4
            D4 D4 E4 C4 D4 E4 F4 E4 C4 D4 E4 F4 E4 D4 C4 D4 G3
            E4 E4 F4 G4 G4 F4 E4 D4 C4 C4 D4 E4 D4 C4 C4"),

        new Song("Fur Elise", @"
            E5 D#5 E5 D#5 E5 B4 D5 C5 A4 C4 E4 A4 B4 E4 G#4 B4 C5
            E4 E5 D#5 E5 D#5 E5 B4 D5 C5 A4 C4 E4 A4 B4 E4 C5 B4 A4
            B4 C5 D5 E5 G4 F5 E5 D5 F4 E5 D5 C5 E4 D5 C5 B4
            E4 E5 D#5 E5 D#5 E5 D#5 E5 B4 D5 C5 A4 C4 E4 A4 B4 E4 G#4 B4 C5
            E4 E5 D#5 E5 D#5 E5 B4 D5 C5 A4 C4 E4 A4 B4 E4 C5 B4 A4"),

        new Song("Canon in D", @"
            F#5 E5 D5 C#5 B4 A4 B4 C#5
            D5 C#5 B4 A4 G4 F#4 G4 E4
            D4 F#4 A4 G4 F#4 D4 F#4 E4 D4 B3 D4 A4 G4 B4 A4 G4
            F#4 D4 E4 C#5 D5 F#5 A5 A4 B4 G4 A4 F#4 D4 D5 D5 C#5
            D5 C#5 D5 D4 C#4 A4 E4 F#4 D4 D5 C#5 B4 C#5 F#5 A5 B5
            G5 F#5 E5 G5 F#5 E5 D5 C#5 B4 A4 G4 F#4 E4 G4 F#4 E4 D4"),

        new Song("Turkish March", @"
            B4 A4 G#4 A4 C5 D5 C5 B4 C5 E5
            F5 E5 D#5 E5 B5 A5 G#5 A5 B5 A5 G#5 A5 C6
            A5 C6 B5 A5 G5 A5 B5 A5 G5 A5 B5 A5 G5 F#5 E5
            B4 A4 G#4 A4 C5 D5 C5 B4 C5 E5
            F5 E5 D#5 E5 B5 A5 G#5 A5 B5 A5 G#5 A5 C6
            A5 B5 C6 B5 A5 G#5 A5 E5 F5 D5 C5 B4 A4"),

        new Song("Hall of the Mountain King", @"
            B3 C#4 D4 E4 F#4 D4 F#4 F4 C#4 F4 E4 C4 E4
            B3 C#4 D4 E4 F#4 D4 F#4 B4 A4 F#4 D4 F#4 A4
            B3 C#4 D4 E4 F#4 D4 F#4 F4 C#4 F4 E4 C4 E4
            B3 C#4 D4 E4 F#4 D4 F#4 B4 A4 F#4 D4 F#4 A4
            F#4 G#4 A#4 B4 C#5 A#4 C#5 D5 A#4 D5 C#5 A#4 C#5
            F#4 G#4 A#4 B4 C#5 A#4 C#5 D5 A#4 D5 C#5"),

        new Song("Minuet in G", @"
            D5 G4 A4 B4 C5 D5 G4 G4
            E5 C5 D5 E5 F#5 G5 G4 G4
            C5 D5 C5 B4 A4 B4 C5 B4 A4 G4
            F#4 G4 A4 B4 G4 B4 A4
            D5 G4 A4 B4 C5 D5 G4 G4
            E5 C5 D5 E5 F#5 G5 G4 G4
            C5 D5 C5 B4 A4 B4 C5 B4 A4 G4
            A4 B4 A4 G4 F#4 G4"),

        new Song("Twinkle Twinkle", @"
            C4 C4 G4 G4 A4 A4 G4 F4 F4 E4 E4 D4 D4 C4
            G4 G4 F4 F4 E4 E4 D4 G4 G4 F4 F4 E4 E4 D4
            C4 C4 G4 G4 A4 A4 G4 F4 F4 E4 E4 D4 D4 C4"),
    };
}
