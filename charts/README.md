# Double playback speed of a video
```
ffmpeg -i input.mp4 -filter:v "setpts=0.5*PTS" output.mp4
```

# GIF creation cmd reference
```
ffmpeg -ss 12 -t 13 -i input.mp4 -vf "fps=10,scale=480:-1" -loop 0 output.gif
```

```
ffmpeg -i input.mp4 -vf "fps=10,scale=480:-1,setpts=0.5*PTS" -loop 0 output.gif
```

# Extract GIF (with alpha channel, e.g. exported from Fighter Factory Studio) to PNG sequence
```
ffmpeg -vsync vfr -i input.gif output%d.png
```

The `-vsync vfr` tells ffmpeg to disrespect the original delays set within the GIF file, otherwise many duplicate frame will be extracted by the default 60FPS.

More complicated transparent padding example (used when alignment in image source is much more preferred than aligning in codes) 
```
ffmpeg -vsync vfr -i LayDown1.gif -vf "scale=iw:188:force_original_aspect_ratio=decrease,pad=iw:188:0:(oh-ih):color=#00000000,format=rgba" pngs/LayDown1_%d.png
```

The command above uses same input-output width, but pads the output height with a top transparent section such that the output height is fixed to 188px. 

Similarly to crop a gif into pngs.
```
ffmpeg -vsync vfr -i Idle1.gif -vf "crop=70:ih:(iw-ow-10):0" pngs/Idle1_%d.png
```

It can also crop an audio file, see below the sample command to crop only the first 30s of the original BGM.
```
ffmpeg -ss 0 -t 30 -i .\bgm.mp3 .\bgm_short.mp3
```

To speed up audio by 2x.
```
ffmpeg -i .\bgm.mp3 -filter:a "atempo=2.0" .\bgm_fast.mp3
```
