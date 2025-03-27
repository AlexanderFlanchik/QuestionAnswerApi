
# Simple Questions/Answers API

This is a minimal .NET API that provides a simple way to handle questions and answers. It uses JavaScript handlers for processing HTTP requests. The integration with JavaScript is achieved through the use of the **Jint** library, which allows executing JavaScript code directly within the .NET environment.

## Features

- **Minimal API**: Simple .NET 9+ API with minimal boilerplate.
- **JavaScript Handlers**: HTTP request handling logic is extended with JavaScript for dynamic behavior.
- **Jint Library**: JavaScript execution integrated using the [Jint](https://github.com/sebastienros/jint) library.

## Setup & Installation

To get started with the project, follow the instructions below:

### Prerequisites

- .NET 9.0 or higher
- A code editor like [Visual Studio Code](https://code.visualstudio.com/) (or Visual Studio)
- [Jint](https://github.com/sebastienros/jint) NuGet package

### Steps to Install

1. **Clone the Repository:**

   ```bash
   git clone https://github.com/AlexanderFlanchik/QuestionAnswerApi.git
   cd QuestionAsnwerApi
2. **Restore and run**
    dotnet restore
    dotnet run
