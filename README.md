# 🌾 Kisan Mitra AI

> Empowering Farmers with AI-Powered Agricultural Intelligence

[![Watch Demo](https://img.shields.io/badge/▶️_Watch_Demo-FF0000?style=for-the-badge&logo=youtube&logoColor=white)](https://youtu.be/YUFBD3C5Ed8)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![AWS](https://img.shields.io/badge/AWS-Cloud-FF9900?style=flat-square&logo=amazon-aws)](https://aws.amazon.com/)
[![License](https://img.shields.io/badge/License-Proprietary-blue?style=flat-square)](LICENSE)

---

## 📖 Table of Contents

- [What is Kisan Mitra AI?](#-what-is-kisan-mitra-ai)
- [Why Kisan Mitra AI?](#-why-kisan-mitra-ai)
- [Key Features](#-key-features)
- [How It Works](#-how-it-works)
- [Technology Stack](#-technology-stack)
- [Project Structure](#-project-structure)
- [Getting Started](#-getting-started)
- [Configuration Guide](#-configuration-guide)
- [Usage Examples](#-usage-examples)
- [Deployment](#-deployment)
- [Contributing](#-contributing)
- [License](#-license)

---

## 🌟 What is Kisan Mitra AI?

**Kisan Mitra AI** (Farmer's Friend AI) is a comprehensive, AI-powered agricultural platform designed to support farmers throughout the entire farming lifecycle. It combines cutting-edge artificial intelligence, voice recognition, computer vision, and data analytics to provide farmers with actionable insights in their local language.

Think of it as a **digital agricultural advisor** that farmers can talk to, show pictures to, and get instant, personalized recommendations for their crops, soil, and market prices.

### 🎥 Watch the Demo

**See Kisan Mitra AI in action:** [https://youtu.be/YUFBD3C5Ed8](https://youtu.be/YUFBD3C5Ed8)

**See the Blog On Builder:** [https://builder.aws.com/content/3AnYuaQB0anjlhInVc8rkljE1mZ/empowering-indian-farmers-with-amazon-nova-pro-building-kisan-mitra-ai](https://builder.aws.com/content/3AnYuaQB0anjlhInVc8rkljE1mZ/empowering-indian-farmers-with-amazon-nova-pro-building-kisan-mitra-ai)

---

## 💡 Why Kisan Mitra AI?

### The Problem

Farmers face numerous challenges:
- 🗣️ **Language Barriers**: Most agricultural information is in English, not regional languages
- 📊 **Information Gap**: Difficulty accessing real-time market prices and weather data
- 🌱 **Soil Health**: Lack of affordable soil testing and analysis
- 📅 **Planting Decisions**: Uncertainty about what to plant and when
- 💰 **Pricing**: Difficulty in grading produce quality and getting fair prices

### Our Solution

Kisan Mitra AI bridges these gaps by providing:
- ✅ **Voice-First Interface**: Speak in your local language (Hindi, Punjabi, Bengali, etc.)
- ✅ **AI-Powered Insights**: Get instant answers powered by advanced AI
- ✅ **Visual Analysis**: Take a photo to grade crop quality or analyze soil health cards
- ✅ **Real-Time Data**: Access live market prices (Mandi rates) and weather forecasts
- ✅ **Personalized Recommendations**: Get crop suggestions based on your location and soil

---

## 🚀 Key Features

### 1. 🎤 Krishi-Vani (Voice Intelligence)
**Talk to your AI farming assistant**
- Speak naturally in Hindi, English, or regional dialects
- Ask about market prices, weather, crop diseases, or farming techniques
- Get voice responses in your preferred language
- Example: *"Aaj ke aloo ke bhav kya hain Delhi mein?"* (What are today's potato prices in Delhi?)

### 2. 📸 Quality Grader (Vision AI)
**Grade your produce with a photo**
- Take a picture of your fruits or vegetables
- AI analyzes quality, size, color, and defects
- Get instant quality grade (A, B, C) and estimated market price
- Supports: Potatoes, Tomatoes, Onions, Apples, and more

### 3. 🌱 Dhara-Analyzer (Soil Intelligence)
**Digitize your soil health card**
- Upload a photo of your soil health card
- AI extracts all nutrient data automatically
- Get personalized fertilizer recommendations
- Receive regenerative farming plans for soil improvement

### 4. 🌾 Sowing Oracle (Planting Advisor)
**Know what to plant and when**
- Get crop recommendations based on your location
- Optimal planting windows for each crop
- Seed variety suggestions
- Expected yield and market demand forecasts

---

## 🔧 How It Works

### Architecture Overview

```
┌─────────────┐
│   Farmer    │ (Voice/Photo/Text Input)
└──────┬──────┘
       │
       ▼
┌─────────────────────────────────────┐
│     React Frontend (Web/Mobile)     │
│  - Voice Recording                  │
│  - Image Upload                     │
│  - Real-time Chat                   │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│    .NET 8 Backend API (AWS Lambda)  │
│  - Authentication (Cognito)         │
│  - Request Routing                  │
│  - Business Logic                   │
└──────────────┬──────────────────────┘
               │
       ┌───────┴───────┐
       ▼               ▼
┌─────────────┐  ┌─────────────┐
│  AWS AI     │  │  Databases  │
│  Services   │  │             │
│             │  │ - DynamoDB  │
│ - Transcribe│  │ - S3        │
│ - Bedrock   │  │ - Timestream│
│ - Rekognition│ │             │
│ - Textract  │  │             │
│ - Polly     │  │             │
└─────────────┘  └─────────────┘
```

### Request Flow Example

1. **Farmer speaks**: "What's the potato price in Delhi?"
2. **Frontend** records audio and sends to backend
3. **AWS Transcribe** converts speech to text
4. **AWS Bedrock (Claude AI)** understands the query
5. **Backend** fetches data from DynamoDB (Mandi prices)
6. **AWS Bedrock** generates a natural response
7. **AWS Polly** converts text to speech
8. **Frontend** plays audio response to farmer

---

## 🛠️ Technology Stack

### Backend
- **Framework**: .NET 8 (C#)
- **API**: ASP.NET Core Web API
- **Architecture**: Clean Architecture (Core, Infrastructure, API layers)
- **Testing**: xUnit with FsCheck (property-based testing)

### AI & Cloud Services (AWS)
- **Amazon Transcribe**: Speech-to-text (voice input)
- **Amazon Bedrock**: Claude 3.5 Sonnet (AI reasoning)
- **Amazon Rekognition**: Image analysis (quality grading)
- **Amazon Textract**: OCR (soil health card extraction)
- **Amazon Polly**: Text-to-speech (voice output)
- **Amazon S3**: Object storage (images, audio files)
- **Amazon DynamoDB**: NoSQL database (prices, user data)
- **Amazon Timestream**: Time-series data (weather, prices)
- **AWS Lambda**: Serverless compute
- **Amazon Cognito**: User authentication
- **AWS Step Functions**: Workflow orchestration

### Frontend
- **Framework**: React with TypeScript
- **State Management**: Redux Toolkit
- **UI Library**: Material-UI / Tailwind CSS
- **Audio Recording**: Web Audio API

### Integration
- **SOAP Services**: CoreWCF (government system integration)
- **REST APIs**: External weather and market data

---

## 📁 Project Structure

```
KisanMitraAI/
│
├── src/
│   ├── KisanMitraAI.API/              # 🌐 Web API & Entry Point
│   │   ├── Controllers/               # API endpoints
│   │   ├── Middleware/                # JWT, CORS, error handling
│   │   ├── Program.cs                 # Application startup
│   │   └── appsettings.json           # Configuration
│   │
│   ├── KisanMitraAI.Core/             # 🧠 Business Logic
│   │   ├── Models/                    # Domain entities
│   │   ├── Interfaces/                # Service contracts
│   │   └── Services/                  # Business rules
│   │
│   └── KisanMitraAI.Infrastructure/   # 🔌 External Integrations
│       ├── AI/                        # AWS Bedrock integration
│       ├── Vision/                    # Rekognition, Textract
│       ├── Voice/                     # Transcribe, Polly
│       ├── Storage/                   # S3 services
│       ├── Repositories/              # DynamoDB, Timestream
│       └── PlantingAdvisory/          # Crop recommendations
│
├── react-frontend/                    # 💻 React Web Application
│   ├── src/
│   │   ├── components/                # Reusable UI components
│   │   ├── pages/                     # Page components
│   │   ├── services/                  # API clients
│   │   └── store/                     # Redux state management
│   └── package.json
│
│
├── KisanMitraAI.sln                   # Visual Studio Solution
├── .gitignore                         # Git ignore rules
└── README.md                          # This file
```

---

## 🚀 Getting Started

### Prerequisites

Before you begin, ensure you have the following installed:

1. **Development Tools**
   - [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (version 8.0 or higher)
   - [Node.js](https://nodejs.org/) (version 18+ for React frontend)
   - [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)
   - [Git](https://git-scm.com/)

2. **AWS Account**
   - Active AWS account with billing enabled
   - AWS CLI installed and configured
   - IAM user with appropriate permissions

3. **Optional**
   - [Postman](https://www.postman.com/) for API testing
   - [AWS Toolkit](https://aws.amazon.com/visualstudiocode/) for VS Code

### Installation Steps

#### Step 1: Clone the Repository

```bash
git clone https://github.com/Rajatsahu0/Kisan-Mirta-AI.git
cd Kisan-Mirta-AI
```

#### Step 2: Install Backend Dependencies

```bash
# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build
```

#### Step 3: Install Frontend Dependencies

```bash
cd react-frontend
npm install
cd ..
```

#### Step 4: Configure AWS Credentials

**Option A: Using AWS CLI**
```bash
aws configure
# Enter your AWS Access Key ID
# Enter your AWS Secret Access Key
# Enter your default region (e.g., us-east-1)
```

**Option B: Using Environment Variables**
```bash
# Windows (PowerShell)
$env:AWS_ACCESS_KEY_ID="your-access-key"
$env:AWS_SECRET_ACCESS_KEY="your-secret-key"
$env:AWS_REGION="us-east-1"

# Linux/Mac
export AWS_ACCESS_KEY_ID="your-access-key"
export AWS_SECRET_ACCESS_KEY="your-secret-key"
export AWS_REGION="us-east-1"
```

#### Step 5: Configure Application Settings

1. Copy the example configuration:
```bash
cp src/KisanMitraAI.API/appsettings.example.json src/KisanMitraAI.API/appsettings.json
```

2. Edit `src/KisanMitraAI.API/appsettings.json` with your AWS details:
```json
{
  "AWS": {
    "Region": "us-east-1",
    "AccessKey": "your-access-key",
    "SecretKey": "your-secret-key"
  },
  "Cognito": {
    "UserPoolId": "your-user-pool-id",
    "ClientId": "your-client-id"
  },
  "S3": {
    "BucketName": "your-bucket-name"
  }
}
```

#### Step 6: Set Up AWS Services

Run the setup scripts to create required AWS resources:

```bash
# Create DynamoDB tables
.\create-userprofiles-table.ps1
.\create-voice-history-table.ps1

# Configure S3 buckets
.\configure-s3-cors.ps1

# Set up Cognito user pool
.\configure-cognito-sms.ps1
```

#### Step 7: Run the Application

**Backend API:**
```bash
dotnet run --project src/KisanMitraAI.API
```
The API will start at `https://localhost:5001`

**Frontend (in a new terminal):**
```bash
cd react-frontend
npm start
```
The frontend will open at `http://localhost:3000`

#### Step 8: Verify Installation

1. Open your browser to `http://localhost:3000`
2. Register a new account
3. Try the voice query feature
4. Upload a test image for quality grading

---

## ⚙️ Configuration Guide

### AWS Services Setup

#### 1. Amazon Cognito (User Authentication)
```bash
# Create user pool
aws cognito-idp create-user-pool --pool-name KisanMitraAI-Users

# Create app client
aws cognito-idp create-user-pool-client \
  --user-pool-id <your-pool-id> \
  --client-name KisanMitraAI-Web
```

#### 2. Amazon S3 (File Storage)
```bash
# Create bucket
aws s3 mb s3://kisanmitraai-storage

# Enable CORS
aws s3api put-bucket-cors \
  --bucket kisanmitraai-storage \
  --cors-configuration file://cors-config.json
```

#### 3. Amazon DynamoDB (Database)
```bash
# Create tables
aws dynamodb create-table \
  --table-name MandiPrices \
  --attribute-definitions AttributeName=CropName,AttributeType=S \
  --key-schema AttributeName=CropName,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST
```

#### 4. Amazon Bedrock (AI Model Access)
- Go to AWS Console → Bedrock
- Request access to Claude 3.5 Sonnet model
- Wait for approval (usually instant)

### Environment Variables

Create a `.env` file in `react-frontend/`:

```env
REACT_APP_API_URL=https://your-api-gateway-url
REACT_APP_AWS_REGION=us-east-1
REACT_APP_COGNITO_USER_POOL_ID=your-pool-id
REACT_APP_COGNITO_CLIENT_ID=your-client-id
REACT_APP_S3_BUCKET=your-bucket-name
```

---

## 📚 Usage Examples

### Example 1: Voice Query (Krishi-Vani)

```javascript
// User speaks: "What's the potato price in Delhi?"

// Backend processes:
1. Transcribe audio → "What's the potato price in Delhi?"
2. Query DynamoDB for latest potato prices in Delhi
3. Generate response: "Today's potato price in Delhi is ₹25 per kg"
4. Convert to speech and return audio
```

### Example 2: Quality Grading

```javascript
// User uploads potato image

// Backend processes:
1. Upload image to S3
2. Analyze with Rekognition (detect defects, size, color)
3. Calculate quality score
4. Return grade: "Grade A - ₹30-35 per kg"
```

### Example 3: Soil Analysis

```javascript
// User uploads soil health card photo

// Backend processes:
1. Extract text with Textract
2. Parse nutrient values (N, P, K, pH, etc.)
3. Generate recommendations with Bedrock AI
4. Return fertilizer plan and crop suggestions
```

---

## 🚢 Deployment

### Deploy to AWS Lambda

```bash
# Build and deploy
.\DEPLOY_TO_AWS.ps1

# Or manually:
dotnet publish -c Release
cd src/KisanMitraAI.API/bin/Release/net8.0/publish
zip -r deployment.zip .
aws lambda update-function-code \
  --function-name KisanMitraAI-API \
  --zip-file fileb://deployment.zip
```

### Deploy Frontend to S3 + CloudFront

```bash
cd react-frontend
npm run build
aws s3 sync build/ s3://your-frontend-bucket
aws cloudfront create-invalidation \
  --distribution-id YOUR_DIST_ID \
  --paths "/*"
```

### Infrastructure as Code (AWS CDK)

```bash
cd infrastructure
npm install
cdk deploy --all
```

---

## 🧪 Testing

### Run Unit Tests

```bash
dotnet test
```

### Run Integration Tests

```bash
dotnet test --filter Category=Integration
```

### Test API Endpoints

```bash
# Test authentication
.\test-aws-integration.ps1

# Test voice query
.\test-voice-query.ps1

# Test quality grading
.\test-quality-grading.ps1
```

---

## 🤝 Contributing

We welcome contributions! Please follow these steps:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Coding Standards
- Follow C# coding conventions
- Write unit tests for new features
- Update documentation as needed
- Ensure all tests pass before submitting PR

---

## 📄 License

Copyright © 2026 Kisan Mitra AI Team. All rights reserved.

This project is proprietary software. Unauthorized copying, distribution, or modification is prohibited.

---

## 📞 Support & Contact

- **Issues**: [GitHub Issues](https://github.com/Rajatsahu0/Kisan-Mirta-AI/issues)
- **Discussions**: [GitHub Discussions](https://github.com/Rajatsahu0/Kisan-Mirta-AI/discussions)
- **Email**: support@kisanmitraai.com

---

## 🙏 Acknowledgments

- AWS for providing cloud infrastructure
- Anthropic for Claude AI model
- Open-source community for various libraries and tools
- Farmers who provided feedback and testing

---

## 🗺️ Roadmap

- [ ] Multi-language support (10+ Indian languages)
- [ ] Mobile app (iOS & Android)
- [ ] Offline mode for remote areas
- [ ] Integration with government schemes
- [ ] Crop disease detection
- [ ] Weather-based alerts
- [ ] Community marketplace

---

**Made with ❤️ for Indian Farmers**

[![Watch Demo](https://img.shields.io/badge/▶️_Watch_Demo-FF0000?style=for-the-badge&logo=youtube&logoColor=white)](https://youtu.be/YUFBD3C5Ed8)
