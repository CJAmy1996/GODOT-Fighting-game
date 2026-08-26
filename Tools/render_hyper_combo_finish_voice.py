from pathlib import Path
import wave

import numpy as np


ROOT = Path(__file__).parents[1]
SOURCE = ROOT / "Assets" / "Audio" / "Announcer" / "hyper_combo_finish_raw.wav"
OUTPUT = ROOT / "Assets" / "Audio" / "Announcer" / "hyper_combo_finish.wav"


with wave.open(str(SOURCE), "rb") as source:
    channels = source.getnchannels()
    sample_width = source.getsampwidth()
    sample_rate = source.getframerate()
    samples = np.frombuffer(source.readframes(source.getnframes()), dtype="<i2").astype(np.float64)

if channels > 1:
    samples = samples.reshape(-1, channels).mean(axis=1)

# Trim the long synthesizer silence, but retain a short dramatic lead-in.
active = np.flatnonzero(np.abs(samples) > 180)
if active.size:
    samples = samples[max(0, active[0] - int(sample_rate * 0.03)):active[-1] + int(sample_rate * 0.12)]

# Slightly lower/lengthen the voice for a larger arcade-announcer chest tone.
new_length = round(len(samples) * 1.075)
samples = np.interp(
    np.linspace(0, len(samples) - 1, new_length),
    np.arange(len(samples)),
    samples,
)

# Project the call with gentle saturation and two short arena reflections.
samples = np.tanh(samples / 9000.0) * 18000.0
tail = int(sample_rate * 0.34)
wet = np.zeros(len(samples) + tail, dtype=np.float64)
wet[:len(samples)] += samples
for delay_seconds, gain in ((0.085, 0.28), (0.17, 0.16), (0.255, 0.08)):
    delay = int(sample_rate * delay_seconds)
    wet[delay:delay + len(samples)] += samples * gain

wet = np.concatenate((np.zeros(int(sample_rate * 0.055)), wet))
wet *= 0.94 * 32767.0 / max(1.0, np.max(np.abs(wet)))
encoded = np.clip(wet, -32768, 32767).astype("<i2")

with wave.open(str(OUTPUT), "wb") as output:
    output.setnchannels(1)
    output.setsampwidth(2)
    output.setframerate(sample_rate)
    output.writeframes(encoded.tobytes())
