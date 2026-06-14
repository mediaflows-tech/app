export function formatAlarmName(name: string): string {
  // EB auto-generated: awseb-e-{id}-stack-AWSEBCloudwatchAlarm{Type}-{hash}
  const ebMatch = name.match(/AWSEBCloudwatchAlarm(High|Low)-/i)
  if (ebMatch) {
    return `EB Auto-Scaling (${ebMatch[1]})`
  }

  // Terraform-defined: mediaflows-{name}-{env}
  return name
    .replace(/^mediaflows-/i, '')
    .replace(/-(prod|dev|staging)$/i, '')
    .split('-')
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ')
}
