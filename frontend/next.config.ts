import type { NextConfig } from 'next'

const nextConfig: NextConfig = {
  // Standalone output for Amplify SSR
  output: 'standalone',

  images: {
    remotePatterns: [
      {
        protocol: 'https',
        hostname: 'cdn.example.com'
      },
      {
        protocol: 'https',
        hostname: '*.s3.ap-southeast-1.amazonaws.com'
      },
      {
        protocol: 'https',
        hostname: '*.cloudfront.net'
      }
    ]
  },

  async rewrites() {
    const apiDestination = process.env.API_BASE_URL || 'https://api.example.com'

    return [
      {
        source: '/api/v1/:path*',
        destination: `${apiDestination}/api/v1/:path*`
      },
      // SignalR WebSocket/SSE connections
      {
        source: '/hubs/:path*',
        destination: `${apiDestination}/hubs/:path*`
      }
    ]
  },

  async headers() {
    return [
      {
        source: '/(.*)',
        headers: [
          {
            key: 'X-Content-Type-Options',
            value: 'nosniff'
          },
          {
            key: 'Referrer-Policy',
            value: 'strict-origin-when-cross-origin'
          },
          {
            key: 'Permissions-Policy',
            value: 'camera=(), microphone=(), geolocation=(), interest-cohort=()'
          },
          {
            key: 'X-DNS-Prefetch-Control',
            value: 'on'
          },
          {
            key: 'Strict-Transport-Security',
            value: 'max-age=63072000; includeSubDomains; preload'
          },
          {
            key: 'Content-Security-Policy',
            value: [
              "default-src 'self'",
              "script-src 'self' 'unsafe-inline' 'unsafe-eval'",
              "style-src 'self' 'unsafe-inline'",
              "img-src 'self' data: blob: https://cdn.example.com https://*.cloudfront.net https://*.s3.ap-southeast-1.amazonaws.com",
              "media-src 'self' blob: https://cdn.example.com https://*.cloudfront.net https://*.s3.ap-southeast-1.amazonaws.com",
              "font-src 'self' data:",
              "connect-src 'self' https://api.example.com wss://api.example.com https://cognito-idp.ap-southeast-1.amazonaws.com https://login.example.com https://*.s3.ap-southeast-1.amazonaws.com",
              'frame-src https://*.s3.ap-southeast-1.amazonaws.com https://cdn.example.com https://*.cloudfront.net',
              "frame-ancestors 'none'",
              "base-uri 'self'",
              "form-action 'self' https://login.example.com"
            ].join('; ')
          }
        ]
      }
    ]
  },

  // Enable source maps in production for error tracking
  productionBrowserSourceMaps: false,

  // Include .env.local in standalone output for SSR runtime
  outputFileTracingIncludes: {
    '/*': ['./.env.local']
  },

  // Experimental features
  experimental: {
    // Optimize server component payloads
    serverActions: {
      bodySizeLimit: '2mb'
    }
  }
}

export default nextConfig
