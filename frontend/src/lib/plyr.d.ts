declare module 'plyr' {
  export type PlyrControl =
    | 'play-large'
    | 'play'
    | 'progress'
    | 'current-time'
    | 'mute'
    | 'volume'
    | 'captions'
    | 'settings'
    | 'pip'
    | 'airplay'
    | 'fullscreen'

  export interface PlyrOptions {
    controls?: PlyrControl[]
    tooltips?: { controls?: boolean; seek?: boolean }
    keyboard?: { focused?: boolean; global?: boolean }
    seekTime?: number
    title?: string
  }

  export default class Plyr {
    constructor(target: string | HTMLElement, options?: PlyrOptions)
    destroy(): void
    play(): void
    pause(): void
  }
}
