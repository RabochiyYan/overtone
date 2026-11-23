"""Render the original 48-second Overtone instrumental loop; standard library only."""
from array import array
from math import sin, pi, exp, sqrt
from pathlib import Path
import wave

RATE = 22050
BEAT = 0.75  # 80 BPM, 16 bars of 4/4.
LENGTH = 48
COUNT = RATE * LENGTH
left, right = array('f', [0]) * COUNT, array('f', [0]) * COUNT


def note(start, duration, midi, gain, pan=0, voice='pad'):
    frequency = 440 * 2 ** ((midi - 69) / 12)
    first = round(start * RATE)
    frames = round(duration * RATE)
    lg, rg = sqrt((1-pan)/2), sqrt((1+pan)/2)
    for i in range(frames):
        t = i / RATE
        phase = 2*pi*frequency*t
        if voice == 'pad':
            envelope = sin(pi*i/frames)**2
            value = (sin(phase)+0.15*sin(phase*2)+0.08*sin(phase*3))/1.23
        elif voice == 'pluck':
            envelope = min(1, t/.012)*exp(-t*5)*min(1, (duration-t)/.05)
            value = .8*sin(phase)+.2*sin(phase*2)
        elif voice == 'bass':
            envelope = min(1,t/.04)*exp(-t*1.8)*min(1,(duration-t)/.12)
            value = sin(phase)+.12*sin(phase*2)
        else:
            envelope = min(1,t/.005)*exp(-t*24)*min(1,(duration-t)/.025)
            value = sin(2*pi*(48*t+1.2*(1-exp(-t*35))))
        index = (first+i) % COUNT
        value *= gain*envelope
        left[index] += value*lg
        right[index] += value*rg


chords = [(45,52,57,60), (41,48,57,60), (48,55,60,64), (40,47,55,59)]
melodies = [(69,72,76,72), (69,72,77,76), (67,72,76,74), (67,71,76,71)]
for bar in range(16):
    chord = chords[bar % 4]
    start = bar * 4 * BEAT
    for part, pitch in enumerate(chord):
        note(start-.2, 4*BEAT+.4, pitch+12, .075, (part-1.5)*.35)
    note(start, 2.5*BEAT, chord[0]-12, .18, voice='bass')
    note(start+2*BEAT, 1.5*BEAT, chord[0]-12, .12, voice='bass')
    for step in range(8):
        note(start+step*BEAT/2, .65, chord[(step+bar//4)%4]+12, .075, -.35 if step%2 else .35, 'pluck')
    for step, pitch in enumerate(melodies[bar%4]):
        if (bar+step)%4 != 3:
            note(start+(step+.25)*BEAT, 1.4, pitch, .115 if bar>=4 else .075, -.12, 'pluck')
    for beat in (0, 2):
        note(start+beat*BEAT, .22, 0, .065, voice='kick')

# Circular stereo echoes preserve the tail across the loop boundary.
delay = round(BEAT/2*RATE)
peak = 0
for i in range(COUNT):
    peak = max(peak, abs(left[i]+.2*right[(i-delay)%COUNT]+.1*left[(i-2*delay)%COUNT]),
               abs(right[i]+.2*left[(i-delay)%COUNT]+.1*right[(i-2*delay)%COUNT]))
scale = .75/max(peak, .001)
output = array('h')
for i in range(COUNT):
    output.append(round((left[i]+.2*right[(i-delay)%COUNT]+.1*left[(i-2*delay)%COUNT])*scale*32767))
    output.append(round((right[i]+.2*left[(i-delay)%COUNT]+.1*right[(i-2*delay)%COUNT])*scale*32767))
path = Path(__file__).resolve().parents[1]/'Assets/Overtone/Resources/OvertoneMusic.wav'
with wave.open(str(path), 'wb') as music:
    music.setnchannels(2)
    music.setsampwidth(2)
    music.setframerate(RATE)
    music.writeframes(output.tobytes())
print(f'Created {path.name}: {LENGTH}s stereo, {path.stat().st_size:,} bytes')
print(f'Loop seam L/R: {abs(output[0]-output[-2])/32767:.5f}, {abs(output[1]-output[-1])/32767:.5f}')
