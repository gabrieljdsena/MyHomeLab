import { useEffect, useRef } from 'react'
import Plyr from 'plyr'
import 'plyr/dist/plyr.css'

export function VideoPlayer({ src, title }: { src: string; title: string }) {
  const videoRef = useRef<HTMLVideoElement>(null)

  useEffect(() => {
    const element = videoRef.current
    if (!element) return
    const player = new Plyr(element, {
      controls: [
        'play-large',
        'play',
        'progress',
        'current-time',
        'mute',
        'volume',
        'settings',
        'pip',
        'fullscreen',
      ],
      tooltips: { controls: true, seek: true },
      keyboard: { focused: true, global: false },
      seekTime: 10,
      title,
    })
    return () => player.destroy()
  }, [src, title])

  return (
    <video
      key={src}
      ref={videoRef}
      src={src}
      playsInline
      preload="metadata"
      className="mx-auto max-h-[60vh] w-full rounded-xl"
    />
  )
}
