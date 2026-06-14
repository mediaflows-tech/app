import type { Metadata } from 'next'
import { GeistSans } from 'geist/font/sans'
import { GeistMono } from 'geist/font/mono'
import { Toaster } from 'react-hot-toast'
import { QueryProvider } from '@/providers/query-provider'
import { SessionProvider } from '@/providers/session-provider'
import { ThemeProvider } from '@/providers/theme-provider'
import './globals.css'

export const metadata: Metadata = {
  metadataBase: new URL('https://web.example.com'),
  title: 'MediaFlows',
  description: 'Digital Asset Management Platform',
  openGraph: {
    title: 'MediaFlows',
    description: 'Digital Asset Management Platform',
    url: 'https://web.example.com',
    siteName: 'MediaFlows',
    images: [
      {
        url: '/embed-banner.jpg',
        width: 1200,
        height: 598,
        alt: 'MediaFlows — Digital Asset Management Platform'
      }
    ],
    locale: 'en_US',
    type: 'website'
  },
  twitter: {
    card: 'summary_large_image',
    title: 'MediaFlows',
    description: 'Digital Asset Management Platform',
    images: ['/embed-banner.jpg']
  }
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className={`${GeistSans.variable} ${GeistMono.variable} font-sans antialiased`}>
        <SessionProvider>
          <ThemeProvider>
            <QueryProvider>
              {children}
              <Toaster
                position="bottom-center"
                toastOptions={{
                  duration: 2500,
                  style: {
                    background: 'var(--color-card)',
                    color: 'var(--color-card-foreground)',
                    border: '1px solid var(--color-border)',
                    borderRadius: '0.75rem',
                    padding: '12px 16px',
                    fontSize: '0.875rem',
                    boxShadow: '0 4px 24px rgba(0,0,0,0.12)',
                    maxWidth: '420px'
                  }
                }}
              />
            </QueryProvider>
          </ThemeProvider>
        </SessionProvider>
      </body>
    </html>
  )
}
