#!/bin/bash
# Install and start SSM Agent if not already running
if ! systemctl is-active --quiet amazon-ssm-agent 2>/dev/null; then
  echo "Installing SSM Agent..."
  sudo dnf install -y amazon-ssm-agent 2>/dev/null || sudo yum install -y amazon-ssm-agent 2>/dev/null || true
  sudo systemctl enable amazon-ssm-agent
  sudo systemctl start amazon-ssm-agent
  echo "SSM Agent installed and started"
else
  echo "SSM Agent already running"
fi
