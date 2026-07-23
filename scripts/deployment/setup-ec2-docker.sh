#!/bin/bash
# DevHunt EC2 Setup Script
# This script installs Docker and prepares Ubuntu 24.04 for DevHunt deployment

set -e

echo "🚀 DevHunt EC2 Setup Script"
echo "==========================="
echo ""

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Update system
echo -e "${YELLOW}📦 Updating system packages...${NC}"
sudo apt update && sudo apt upgrade -y

# Install Docker
echo -e "${YELLOW}🐳 Installing Docker...${NC}"
sudo apt install -y docker.io docker-compose-v2 git curl wget

# Enable Docker service
echo -e "${YELLOW}⚙️  Enabling Docker service...${NC}"
sudo systemctl enable docker
sudo systemctl start docker

# Add current user to docker group
echo -e "${YELLOW}👤 Adding user to docker group...${NC}"
sudo usermod -aG docker $USER

# Install useful tools
echo -e "${YELLOW}🔧 Installing useful tools...${NC}"
sudo apt install -y htop iotop ncdu

# Configure swap (useful for builds)
echo -e "${YELLOW}💾 Configuring swap space...${NC}"
if [ ! -f /swapfile ]; then
    sudo fallocate -l 4G /swapfile
    sudo chmod 600 /swapfile
    sudo mkswap /swapfile
    sudo swapon /swapfile
    echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
    echo -e "${GREEN}✓ 4GB swap created${NC}"
else
    echo -e "${GREEN}✓ Swap already exists${NC}"
fi

# Configure Docker logging (prevent disk fill)
echo -e "${YELLOW}📝 Configuring Docker logging...${NC}"
sudo mkdir -p /etc/docker
cat << EOF | sudo tee /etc/docker/daemon.json
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  }
}
EOF
sudo systemctl restart docker

# Create project directory
echo -e "${YELLOW}📁 Creating project directory...${NC}"
mkdir -p ~/devhunt

# Show versions
echo ""
echo -e "${GREEN}✅ Setup complete!${NC}"
echo ""
echo "Installed versions:"
docker --version
docker compose version
git --version
echo ""
echo -e "${YELLOW}⚠️  You need to logout and login again for docker group to take effect!${NC}"
echo "   Run: exit"
echo "   Then reconnect via SSH"
echo ""
echo "Next steps:"
echo "1. Upload your project to ~/devhunt"
echo "2. Create .env file with production secrets"
echo "3. Run: cd ~/devhunt && docker compose up -d"
