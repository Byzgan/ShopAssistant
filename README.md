## ShopAssistant – Backend Solution Documentation

## Overview

ShopAssistant is a scalable, modular backend for an online store assistant built on .NET 9.
It leverages state-of-the-art LLM embeddings (LaBSE) and high-performance vector search to enable intelligent FAQ, recommendation, and conversational support for customers and store owners.

## Solution Architecture Overview

- **ChatService**: Main orchestrator for processing chat questions and returning answers.
- **Knowledge Base**: FAQ and knowledge documents, stored in updatable JSON and pre-embedded into binary files.
- **Preprocessing Pipeline (`ShopAssistant.Tools`)**: Generates embeddings for KB entries using LaBSE (ONNX) and Microsoft.ML.Tokenizers. The output is a binary embedding index file.
- **In-Memory Caching (IMemoryCache)**: Used throughout the backend for rapid access to chat history, pending intents, KB answers, and embedding indices.
- **HNSW.Net Vector Search**: Provides high-performance semantic (embedding-based) search over the knowledge base at runtime.
- **Multi-turn Dialog and Intent Handlers**: Enable complex, slot-filling conversations and advanced scenarios, with handler logic mapped to intents defined in updatable JSON.

---


## 🧩 ShopAssistant Main Workflow

```
User
  │
  ▼
[Input Validation & Authentication]
  │
  ▼
[FAQ Cache Lookup]
  │
  ├─► [If FAQ Found] → Return Answer & Log Analytics
  │
  ▼
[Clarification from User?]
  │
  ├─► [If User is Responding to Clarification]
  │        │
  │        └─► Update intent/category context
  │        └─► Resume Intent Handler/Step
  │
  ▼
[Ongoing Multi-Turn Dialog?]
  │
  ├─► [If Yes]
  │        │
  │        └─► Resume Pending Handler Step (slot filling)
  │
  ▼
[Intent Detection]
  │
  ├─► [If Multiple Intents Detected]
  │        │
  │        └─► **Clarification Step**: Prompt user for intent/category selection
  │
  ▼
[Intent Handler Invocation]
  │
  ├─► [If Handler Needs More Data]
  │        │
  │        └─► Multi-turn Dialog/Slot Filling
  │        │      └─► If ambiguous (e.g., multiple categories), **Clarification Step**
  │
  ├─► [If Handler Completes]
  │        └─► Return Final Answer / Handle Analytics / Cache as needed
  │
  ▼
[Semantic KB Search with Role Filtering]
  │
  ├─► [If Found] → Return Answer & Log Analytics
  │
  ▼
[Default Reply]
    └─► Not Found / System Error / Log Analytics

  ▼
[All Steps: Analytics Logging (Intent, FAQ, Clarification, etc.)]

```

---

## Key Workflow: How a Chat Request is Processed

1. **Message Reception & Authentication**
   - User sends a message, which is authenticated via JWT.

2. **FAQ Cache Lookup**
   - If the message matches a cached FAQ or knowledge base entry, the answer is returned immediately.

3. **Ongoing Multi-Turn Dialog Check**
   - If the user is in the middle of a multi-turn dialog (e.g., product search), their message is routed to the appropriate dialog handler.

4. **Intent Detection and Handler Invocation**
   - The system analyzes the message to detect the user’s intent (using pattern matching, stemming, and semantic search).
   - The corresponding handler (e.g., product search, order status, etc.) is executed, possibly initiating or continuing a multi-turn dialog.

5. **Clarification Step**
   - **At any point, if ambiguity is detected**—such as multiple possible intents, multiple matching categories, or any other situation where the system cannot confidently proceed—the assistant triggers a clarification step:
     - The user is prompted to select the intended meaning from a list of likely alternatives.
     - Once clarified, the conversation resumes seamlessly with the correct context.
   - This ensures accuracy and prevents incorrect or unintended actions.

6. **Role-Based Topic Filtering**
   - Only topics or actions permitted by the user’s role are considered throughout processing.

7. **Semantic Knowledge Base Search (if needed)**
   - If no direct answer is available, semantic (vector-based) search is performed within the allowed topics.

8. **Response Construction & Caching**
   - The final answer or next dialog prompt is constructed and returned. Frequently used knowledge is cached for performance.


---

This workflow supports:
- **Fast FAQ responses**
- **Intelligent, multi-turn conversations**
- **Rich intent handling and extensibility**
- **Role-based access control**
- **Easy addition of new dialog scenarios or intents**


---

## Major Technologies & Dependencies

* **.NET 9**
* **Microsoft.ML.OnnxRuntime** (for ONNX LaBSE inference)
* **HNSW.Net** (high-performance in-memory ANN)
* **LaBSE Model**: `labse.onnx`
* **Tokenizer Vocabulary**: `vocab.txt`
* **Embeddings storage**: `.bin` files, memory-mapped on load
* **IVectorStore Abstraction**: simplifies future cloud migration
* **Extensible Intent Processing**: Easily add new intents/handlers


## Configuration

The system is highly configurable via `appsettings.json` and a set of external JSON files for knowledge, intent patterns, permissions, and localization.  
Key settings and their purpose:

- **Languages / Localization**
  - `Languages.Default` and `Languages.Supported`: Set the default and available languages for chat, prompts, and clarification.
  - `LocalizationFilePath`: Path to localization files for multi-language support, including clarification prompts.

- **Knowledge Base & Intent Patterns**
  - `KnowledgeBasePath`: Path to the knowledge base data (for FAQ/semantic search).
  - `IntentPatternsPath`: Path to intent patterns, which drive hybrid intent detection **and** define clarification alternatives when intent is ambiguous.
  - `PermissionsFilePath`: Restricts which knowledge/topics are accessible by role.

- **Embeddings / Semantic Search**
  - `EmbeddingsPath`: Where vector embeddings are stored and loaded for semantic FAQ and intent workflows.
  - `LaBSE.ModelPath`, `LaBSE.VocabPath`: Configures the ONNX model for embeddings.

- **Clarification and Multi-Turn Dialog**
  - **Clarification logic and alternatives are data-driven:**  
    If multiple possible intents or slot (e.g. category) values exist, clarification options are read from the relevant intent pattern or knowledge base JSON—no code changes are needed.
  - Prompts, alternative choices, and logic for clarifications can be updated simply by editing the JSON files.

- **Cache Refresh**
  - Any change to these files can be applied at runtime using the cache refresh controller, without service restart.

- **Security & Logging**
  - `JWT`: Configures authentication/authorization for users.
  - `Logging`: Log levels for the system and API.



---

## Extensibility & Migration

* **IVectorStore** abstraction enables seamless switch from in-memory HNSW.Net to cloud-native vector databases.
* **Intent handlers** can be extended for new business logic.
* Embedding logic supports plugging in other models/providers (including Ollama; support is present but not active).

---

## Temporary & Test Code

* **`ExternalServices`**: Contains dummy classes for external integrations. Not part of production logic; used for development/testing only.
* **`TokenService.cs`** (in `Identity`): Temporary token service. **Not for production use.** Proper production identity/auth solution should be implemented.
* These components are clearly marked in the codebase and should be replaced/removed before production deployment.

---

## Project Structure

```
ShopAssistant/
  ShopAssistant.Api/              # API (Controllers, Program.cs)
  ShopAssistant.Contracts/        # DTOs, shared contracts
  ShopAssistant.Data/             # Data models, embedding storage
  ShopAssistant.Infrastructure/   # Services, caching, integration
  ShopAssistant.IntentProcessing/ # Intent detection & routing
  ShopAssistant.Tests/            # Test projects
  ShopAssistant.Tools/            # CLI/tooling
  ShopAssistant.Utils/            # Utilities (embedding loader, ANN helpers)
  ...
```

---

## Deployment

1. **Clone the repository**  
   - Clone this project to your local machine and switch to the root folder.

2. **Download the LaBSE ONNX model (FP32)**  
   - The backend requires the [LaBSE (Language-agnostic BERT Sentence Embedding)](https://tfhub.dev/google/LaBSE/2) model in ONNX format.  
   - Download `model.onnx` from:  
     https://huggingface.co/sentence-transformers/LaBSE/blob/main/onnx/model.onnx  
   - Place the `model.onnx` file in a folder of your choice.

3. **Download the LaBSE vocabulary file**  
   - Download `vocab.txt` from:  
     https://huggingface.co/sentence-transformers/LaBSE/blob/main/vocab.txt  
   - Place the `vocab.txt` file in a folder of your choice.

4. **Configure application settings**  
   - Open `ShopAssistant.Api/appsettings.json`.  
   - Update:
     - `LaBSE:ModelPath` — path to your model file:  
       - **FP32**: `.../model.onnx`  
       - **INT8 (recommended for CPU)**: `.../laBSE.int8.onnx`
     - `LaBSE:VocabPath` — path to your `vocab.txt`.  
     - Other data file/folder paths as needed.

5. **Prepare the knowledge base and embeddings**  
   - Ensure your knowledge base `.json` files and `.bin` embedding files are present in their configured folders.

6. **Build and run the solution**  
   - Use the .NET 9 SDK to build and run the API project.

   

**Note:**  
- All files and folders referenced in `appsettings.json` must exist and be accessible by the application at runtime.  
- For Linux or Docker deployments, use `/` as the path separator and check file permissions.

---

## Development & Testing

* Unit and integration tests are in the `ShopAssistant.Tests` project.
* Replace all temporary and dummy services before production rollout.
* For local development, dummy external services and token providers allow for rapid prototyping and UI integration.

---
