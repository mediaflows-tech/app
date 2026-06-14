import Image from 'next/image'

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen">
      {/* Brand panel — hidden on mobile */}
      <div className="hidden lg:flex w-[42%] shrink-0 items-center justify-center bg-black p-8">
        <div className="text-center">
          <Image
            src="/mediaflows-light.png"
            alt="MediaFlows"
            width={56}
            height={56}
            priority
            className="mx-auto mb-4 size-14 rounded-xl"
          />
          <div className="text-[1.75rem] font-bold tracking-tight text-white">MediaFlows</div>
          <div className="mt-2 text-[0.9375rem] leading-relaxed text-white/50">
            Media workflows,
            <br />
            simplified.
          </div>
        </div>
      </div>

      {/* Form side */}
      <div className="flex flex-1 items-center justify-center bg-background p-6 lg:p-8">
        <div className="w-full max-w-[340px]">{children}</div>
      </div>
    </div>
  )
}
