import argparse
import math
import random
import struct
import wave
from pathlib import Path


def generate(folder):
    rate = 24000
    tau = 2 * math.pi
    durations = {"TapFlow": 3, "FlushRefill": 2.9, "PumpRotor": 2, "SignIgnition": .32,
                 "SignRelease": .20, "PatrolSiren": 3, "OpeningBirds": 8, "ClosingCrickets": 8, "ClosingKeys": 8}
    for index, (name, seconds) in enumerate(durations.items()):
        rng = random.Random(70 + index)
        data = bytearray()
        low = 0
        for i in range(round(rate * seconds)):
            t = i / rate
            noise = rng.uniform(-1, 1)
            low += .10 * (noise - low)
            if name == "TapFlow":
                value = (.56 * noise + .5 * low) * (.7 + .16 * math.sin(tau * 11 * t)) + .045 * math.sin(tau * 650 * t)
            elif name == "FlushRefill":
                tail = min(1, t * 15) * max(0, 1 - t / seconds)
                value = tail * (.9 * low + .26 * noise + .26 * math.sin(tau * (135*t - 12*t*t) + math.sin(27*t)))
            elif name == "PumpRotor":
                value = .32*math.sin(tau*72*t) + .13*math.sin(tau*144*t) + .09*math.sin(tau*290*t)
            elif name in ("SignIgnition", "SignRelease"):
                value = .75*noise*math.exp(-t*90) + .31*math.exp(-t*16)*math.sin(tau*(1000*t if name=="SignIgnition" else 310*t))
                if name=="SignIgnition": value += .2*math.sin(tau*300*t)*math.exp(-55*abs(t-.13))
            elif name == "PatrolSiren":
                phase = tau*730*t-430*math.cos(tau*t/3)
                value = .40*math.sin(phase)+.075*math.sin(3*phase)
            elif name == "OpeningBirds":
                u = t % 2
                v = (u-.34) % .24
                chirp = math.exp(-v*19) if .34<u<1.03 else 0
                value = .15*low + .33*chirp*math.sin(tau*(1600*v+1200*v*v))
            elif name == "ClosingCrickets":
                pulse = max(0,math.sin(tau*13*t))**3
                value = .11*low+.23*pulse*max(0,math.sin(tau*t/2))*math.sin(tau*2480*t)
            else:
                notes = (55, 62, 67, 65, 58, 62, 69, 65)
                note = notes[int(t) % len(notes)]
                f = 440 * 2 ** ((note-69)/12)
                u=t%1
                value = (.5*math.sin(tau*f*u)+.15*math.sin(tau*2*f*u)) * min(1,u*30)*math.exp(-3.5*u)
            edge = min(1, t/.007, (seconds-t)/.007)
            data.extend(struct.pack("<h",int(32760*max(-.98,min(.98,value*edge)))))
        with wave.open(str(folder/(name+".wav")),"wb") as out:
            out.setparams((1,2,rate,0,"NONE","not compressed"))
            out.writeframes(data)


if __name__ == "__main__":
    parser=argparse.ArgumentParser()
    parser.add_argument("folder",type=Path)
    dest=parser.parse_args().folder
    dest.mkdir(parents=True,exist_ok=True)
    generate(dest)
