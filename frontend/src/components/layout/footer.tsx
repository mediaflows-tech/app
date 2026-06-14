import Link from 'next/link'

export function Footer() {
  return (
    <footer className="px-4 py-6 md:px-6">
      <div className="flex items-center justify-end gap-4 text-xs text-muted-foreground">
        <Link href="/" className="hover:text-foreground transition-colors">
          About
        </Link>
        <span className="text-border">|</span>
        <Link href="/" className="hover:text-foreground transition-colors">
          Contact Us
        </Link>
        <span className="text-border">|</span>
        <span>&copy; {new Date().getFullYear()} MediaFlows</span>
      </div>
    </footer>
  )
}
