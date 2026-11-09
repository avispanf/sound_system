from pathlib import Path
import wave,math,random,struct,sys
project=Path(sys.argv[1]).resolve()
if not (project/'.bad-shift-demo').is_file():raise SystemExit('An isolated BAD SHIFT project is required')
root=project/'Assets/BadShift/Battle';root.mkdir(exist_ok=True)
rng=random.Random(31);sr=44100
for name,duration in [('Attack',.28),('Hit',.22),('Victory',4.8),('Defeat',4.5),('Block',.24),('Challenge',.9)]:
 samples=[]
 for i in range(int(sr*duration)):
  t=i/sr;u=t/duration
  if name=='Attack':v=rng.uniform(-1,1)*math.sin(math.pi*u)**2*.28+math.sin(2*math.pi*(800*t-900*t*t))*.08*(1-u)
  elif name=='Block':v=(math.sin(2*math.pi*820*t)*.4+math.sin(2*math.pi*1320*t)*.22)*math.exp(-t*19)*min(1,t*700)
  elif name=='Hit':v=(math.sin(2*math.pi*175*t)*.5+math.sin(2*math.pi*310*t)*.25+rng.uniform(-1,1)*.16)*math.exp(-t*26)*min(1,t*600)
  else:
   notes={'Victory':[392,493.88,587.33,783.99],'Defeat':[392,329.63,261.63,196],'Challenge':[110,146.83,164.81,220]}[name];index=min(3,int(u*4));local=t-index*duration/4;env=min(1,local/.012)*math.exp(-local*8);f=notes[index];v=(math.sin(2*math.pi*f*local)+.22*math.sin(4*math.pi*f*local))*env*.36
   if name in ('Victory','Defeat'):v+=.12*(math.sin(2*math.pi*notes[0]*t)+.5*math.sin(2*math.pi*notes[2]*t))*math.sin(math.pi*u)**2
  samples.append(int(max(-.9,min(.9,v))*32767))
 with wave.open(str(root/(name+'.wav')),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(sr);w.writeframes(struct.pack('<'+'h'*len(samples),*samples))
