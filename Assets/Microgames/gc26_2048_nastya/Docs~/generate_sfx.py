"""Generates the sound effects of Colours (gc26_2048_nastya) from scratch.

Pure synthesis (sine waves, noise, envelopes); no samples or third-party audio, so the sounds are ours.
Run from the game folder:  python Docs~/generate_sfx.py
Writes Audio/gc26_2048_nastya_*.wav (mono, 44.1 kHz, 16-bit).
"""
import math
import os
import random
import struct
import wave

RATE = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Audio')


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def tone(freq, seconds, decay=8.0, harmonics=((1, 1.0),), attack=0.004, glide=0.0):
    """A plucked/bell-like tone: sum of harmonics with an exponential decay and a short attack.
    glide: relative pitch change over the note (negative = falls)."""
    n = int(RATE * seconds)
    out = []
    phase = [0.0] * len(harmonics)
    for i in range(n):
        t = i / RATE
        f = freq * (1.0 + glide * t / seconds)
        env = math.exp(-decay * t) * min(1.0, t / attack if attack > 0 else 1.0)
        v = 0.0
        for k, (mult, amp) in enumerate(harmonics):
            phase[k] += 2 * math.pi * f * mult / RATE
            v += amp * math.sin(phase[k])
        out.append(v * env)
    return out


def noise_burst(seconds, decay=40.0, smooth=0.85, seed=1):
    """Soft filtered noise: a 'whoosh' when long, a 'tick' when short."""
    rng = random.Random(seed)
    n = int(RATE * seconds)
    out, prev = [], 0.0
    for i in range(n):
        t = i / RATE
        prev = smooth * prev + (1 - smooth) * rng.uniform(-1, 1)
        env = math.exp(-decay * t) * min(1.0, t / 0.003)
        out.append(prev * env)
    return out


def mix(*tracks):
    n = max(len(t) for t in tracks)
    return [sum(t[i] for t in tracks if i < len(t)) for i in range(n)]


def concat(*parts):
    out = []
    for p in parts:
        out += p
    return out


def offset(track, seconds):
    return silence(seconds) + track


def finish(samples, peak=0.8, fade=0.006):
    """Normalize and fade the very end so nothing clicks."""
    m = max(1e-9, max(abs(s) for s in samples))
    samples = [s * peak / m for s in samples]
    f = int(RATE * fade)
    for i in range(min(f, len(samples))):
        samples[-1 - i] *= i / f
    return samples


def write(name, samples, peak):
    path = os.path.join(OUT, 'gc26_2048_nastya_' + name + '.wav')
    data = finish(samples, peak)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, s)) * 32767)) for s in data))
    print(f'{path}  {len(data) / RATE:.2f} s')


BELL = ((1, 1.0), (2, 0.35), (3, 0.12), (4.2, 0.05))
SOFT = ((1, 1.0), (2, 0.2))
C5, E5, G5, C6 = 523.25, 659.25, 783.99, 1046.5
G4, E4, C4, A3 = 392.0, 329.63, 261.63, 220.0


def main():
    os.makedirs(OUT, exist_ok=True)

    # Slide: a short soft swish, quiet (it plays on every move).
    write('Slide', noise_burst(0.09, decay=45, smooth=0.92, seed=3), peak=0.35)

    # Merge: a round pluck. The game raises its pitch for bigger tiles (TDD 10).
    write('Merge', mix(tone(C5, 0.22, decay=16, harmonics=BELL),
                       noise_burst(0.02, decay=200, smooth=0.5, seed=5)), peak=0.7)

    # Invalid move: a dull low bump that falls in pitch.
    write('Invalid', tone(150, 0.14, decay=22, harmonics=SOFT, glide=-0.35), peak=0.6)

    # Win: a bright rising arpeggio with a held top note.
    write('Win', mix(tone(C5, 0.5, decay=7, harmonics=BELL),
                     offset(tone(E5, 0.45, decay=7, harmonics=BELL), 0.09),
                     offset(tone(G5, 0.4, decay=7, harmonics=BELL), 0.18),
                     offset(tone(C6, 0.7, decay=4, harmonics=BELL), 0.27)), peak=0.75)

    # Game over: a soft falling line.
    write('Lose', mix(tone(G4, 0.45, decay=6, harmonics=SOFT),
                      offset(tone(E4, 0.45, decay=6, harmonics=SOFT), 0.16),
                      offset(tone(C4, 0.45, decay=6, harmonics=SOFT), 0.32),
                      offset(tone(A3, 0.8, decay=4, harmonics=SOFT, glide=-0.04), 0.48)), peak=0.7)

    # Button: a tiny click.
    write('Click', mix(tone(1800, 0.035, decay=120, harmonics=SOFT),
                       noise_burst(0.015, decay=300, smooth=0.3, seed=7)), peak=0.4)


if __name__ == '__main__':
    main()
