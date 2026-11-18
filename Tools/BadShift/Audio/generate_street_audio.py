import argparse
import math
import random
import struct
import wave
from pathlib import Path


def render(folder):
    rate = 24000
    frames = rate * 3
    noise = random.Random(26)
    hiss = [noise.uniform(-1, 1) for _ in range(frames)]
    for name in ("NeonTransformer", "PassingMotor"):
        samples = bytearray()
        for i in range(frames):
            t = i / rate
            if name == "NeonTransformer":
                phase = math.tau * 100 * t + .08 * math.sin(math.tau * 2 * t)
                value = .19 * math.sin(phase) + .14 * math.sin(phase * 3) + .034 * math.sin(phase * 12) + .015 * hiss[i]
            else:
                phase = math.tau * 64 * t + .55 * math.sin(math.tau * t)
                rumble = sum(a * math.sin(k * phase) for k, a in ((1, .28), (2, .20), (3, .15), (5, .075), (9, .05)))
                value = rumble + .28 * sum(hiss[(i-j) % frames] for j in range(7)) / 7
            samples.extend(struct.pack("<h", round(32767 * value)))
        with wave.open(str(folder / (name + ".wav")), "wb") as output:
            output.setparams((1, 2, rate, 0, "NONE", "not compressed"))
            output.writeframes(samples)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("folder", type=Path)
    folder = parser.parse_args().folder
    folder.mkdir(parents=True, exist_ok=True)
    render(folder)
