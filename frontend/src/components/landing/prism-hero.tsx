'use client'

import Link from 'next/link'
import { motion, useReducedMotion } from 'motion/react'
import { ArrowRight } from 'lucide-react'
import { WordsPullUp } from './words-pull-up'

const CREAM = '#E1E0CC'

const description =
  'MediaFlows is the workspace where teams gather, review and ship media — from first cut to final approval, in one quiet place.'

export function PrismHero() {
  const reduceMotion = useReducedMotion()

  return (
    <section className="h-screen w-full bg-black">
      <div className="relative h-full w-full overflow-hidden" suppressHydrationWarning>
        {/* Layer 1 — background video (hidden when reduced motion is requested) */}
        {reduceMotion ? (
          // eslint-disable-next-line @next/next/no-img-element -- raw <img> keeps data-testid selector resolving to a plain element; poster is small (~30 KB) and decorative
          <img
            data-testid="hero-poster"
            src="/landing/hero-poster.webp"
            alt=""
            aria-hidden="true"
            className="absolute inset-0 h-full w-full object-cover"
          />
        ) : (
          <video
            data-testid="hero-video"
            autoPlay
            loop
            muted
            playsInline
            preload="metadata"
            poster="/landing/hero-poster.webp"
            src="/landing/hero.mp4"
            aria-hidden="true"
            className="absolute inset-0 h-full w-full object-cover"
          />
        )}

        {/* Layer 2 — film-grain noise */}
        <div
          aria-hidden="true"
          className="noise-overlay pointer-events-none absolute inset-0 mix-blend-overlay opacity-[0.7]"
        />

        {/* Layer 3 — gradient wash */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0 bg-gradient-to-b from-black/30 via-transparent to-black/60"
        />

        {/* Layer 5 — hero content */}
        <div className="absolute bottom-0 left-0 right-0 px-4 pb-2 sm:px-6 md:px-10">
          <div className="grid grid-cols-12 items-end gap-4">
            <div className="col-span-12 lg:col-span-8">
              <h1
                data-testid="hero-wordmark"
                className="font-medium leading-[0.85] tracking-[-0.07em] text-[17vw] sm:text-[16vw] md:text-[15vw] lg:text-[12vw] xl:text-[11.5vw] 2xl:text-[12vw]"
                style={{ color: CREAM }}
              >
                <WordsPullUp text="MediaFlows" />
              </h1>
            </div>

            <div className="col-span-12 flex flex-col gap-5 pb-6 lg:col-span-4 lg:pb-10">
              <motion.p
                initial={reduceMotion ? false : { y: 20, opacity: 0 }}
                animate={{ y: 0, opacity: 1 }}
                transition={{ duration: 0.8, delay: 0.5, ease: [0.16, 1, 0.3, 1] }}
                className="text-xs sm:text-sm md:text-base"
                style={{ color: CREAM, opacity: 0.7, lineHeight: 1.2 }}
              >
                {description}
              </motion.p>

              <motion.div
                initial={reduceMotion ? false : { y: 20, opacity: 0 }}
                animate={{ y: 0, opacity: 1 }}
                transition={{ duration: 0.8, delay: 0.7, ease: [0.16, 1, 0.3, 1] }}
              >
                <Link
                  data-testid="hero-cta-getstarted"
                  href="/login"
                  className="group inline-flex items-center gap-2 self-start rounded-full py-1 pl-5 pr-1 text-sm font-medium transition-all hover:gap-3 sm:text-base focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2"
                  style={{ background: CREAM, color: '#000', outlineColor: '#000' }}
                >
                  Get started
                  <span
                    className="flex h-9 w-9 items-center justify-center rounded-full transition-transform group-hover:scale-110 sm:h-10 sm:w-10"
                    style={{ background: '#000' }}
                  >
                    <ArrowRight className="h-4 w-4" style={{ color: CREAM }} />
                  </span>
                </Link>
              </motion.div>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
