# File Transfer Tool

This project implements a secure and efficient file transfer mechanism with integrity verification, concurrency, and simulated corruption handling. It ensures files are transferred reliably by verifying the integrity of each block using MD5 hashes. If a block fails verification, it is retried with exponential backoff. The project also supports concurrent block transfers for improved performance.

## Features

- **Block-Level Integrity Verification**: Each block is verified using MD5 hashes to ensure data integrity.
- **File Integrity Verification**: Entire file is verified using SHA256 hash mechanism to ensure data integrity.
- **Concurrent Transfers**: Multiple blocks are transferred concurrently for improved performance.
- **Simulated Corruption**: Simulates transient corruption during writing to test the retry mechanism.
- **Retry Mechanism**: Failed blocks are retried with exponential backoff (up to 5 retries).
- **Backup and Restore**: If the transfer fails, the destination file is restored to its original state (if it existed) or deleted (if it was newly created).
- **Logging**: Detailed logs for each block, including position, size, and hash values.

## Requirements

- **.NET Core SDK**: The project is built using .NET Core. Ensure you have the [.NET Core SDK](https://dotnet.microsoft.com/download) installed.
- **Operating System**: Compatible with Windows, macOS, and Linux.

## How to Use

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/your-username/file-transfer-project.git
   cd file-transfer-project
   ```
2. **Open the solution in Visual Studio**
3. **Run the program**
4. **Provide Input**:

   - Enter the source file path when prompted.
   - Enter the destination file path when prompted.

5. **Monitor the Logs**:

The application will log details for each block, including position, size, and hash values. If a block fails verification, it will be retried with exponential backoff.
Logs can also be found in 'transfer-log.txt' file which can be found in 'file-transfer-tool/bin/debug/net9.0/transfer-log.txt'

6. **Verify the Transfer**:

After the transfer completes, the application will verify the integrity of the entire file using SHA-256.
If the hashes match, the transfer is successful. Otherwise, an error is logged.

## Configuration

- **Chunk Size**: The size of each block is randomly chosen between 1 MB and 2 MB.
- **Concurrency**: Up to 4 blocks are transferred concurrently.
- **Corruption Probability**: There is a 10% chance of simulating corruption for each block.
