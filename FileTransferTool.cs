using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

class FileTransferTool
{
    static void Main()
    {
        string sourcePath = ReadUserInputForSourceFilePath("source");
        string destinationPath = ReadUserInputForSourceFilePath("destination");

        try
        {
            TransferFile(sourcePath, destinationPath).Wait();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error during file transfer: " + ex.Message);
        }

    }

    static async Task TransferFile(string sourcePath, string destinationPath)
    {
        const int minChunkSize = 1024 * 1024; // 1 MB
        const int maxChunkSize = 2 * 1024 * 1024; // 2 MB
        const int maxConcurrentTransfers = 4; // Maximum number of concurrent transfers
        Random rng = new Random();

        // Check if the destination file already exists
        bool destinationFileExisted = File.Exists(destinationPath);
        string backupFilePath = null;

        // If the destination file exists, create a backup
        if (destinationFileExisted)
        {
            backupFilePath = CreateBackupFile(destinationPath);
        }

        try
        {
            using (FileStream sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            using (FileStream destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            {
                long totalBytes = sourceStream.Length;
                long bytesTransferred = 0;
                int chunkNumber = 1;

                // List to hold all transfer tasks
                var transferTasks = new List<Task<bool>>();

                while (bytesTransferred < totalBytes || transferTasks.Count > 0)
                {
                    // Start new transfers if we have room in the concurrency limit
                    while (transferTasks.Count < maxConcurrentTransfers && bytesTransferred < totalBytes)
                    {
                        int chunkSize = rng.Next(minChunkSize, maxChunkSize + 1);
                        if (bytesTransferred + chunkSize > totalBytes)
                            chunkSize = (int)(totalBytes - bytesTransferred);

                        byte[] buffer = new byte[chunkSize];
                        int bytesRead = sourceStream.Read(buffer, 0, chunkSize);
                        if (bytesRead == 0) break;

                        // Start a new task for transferring the block
                        var task = TransferBlockWithRetryAsync(destinationStream, buffer, bytesRead, bytesTransferred, chunkNumber);
                        transferTasks.Add(task);

                        bytesTransferred += bytesRead;
                        chunkNumber++;
                    }

                    // Wait for at least one task to complete
                    var completedTask = await Task.WhenAny(transferTasks);
                    transferTasks.Remove(completedTask);

                    // Check if the completed task was successful
                    if (!await completedTask)
                    {
                        Log("A block transfer failed. Aborting transfer.");

                        // If the destination file did not exist before, delete it
                        if (!destinationFileExisted)
                        {
                            DeletePartiallyTransferredFile(destinationStream, destinationPath);
                        }
                        else
                        {
                            RestoreOriginalFileFromBackup(destinationStream, backupFilePath, destinationPath);
                        }

                        return;
                    }
                }
            }

            VerifyFileIntegrity(sourcePath, destinationPath);

            Log("File transfer completed.");
        }
        finally
        {
            CleanupBackupFile(backupFilePath);
        }
    }

    static byte[] SimulateCorruption(byte[] buffer, int chunkNumber)
    {
        const double corruptionProbability = 0.1; // 10% chance of corruption
        Random rng = new Random();
        byte[] corruptedBuffer = null;
        bool simulateCorruption = rng.NextDouble() < corruptionProbability;
        if (simulateCorruption)
        {
            corruptedBuffer = (byte[])buffer.Clone();
            corruptedBuffer[0] ^= 0xFF; // Flip a bit
            Console.WriteLine($"Simulating corruption for Chunk {chunkNumber}...");
        }
        return corruptedBuffer == null ? buffer : corruptedBuffer;
    }

    static double ConvertMegaBytesFromBytes(int bytes)
    {
        const double megabyte = 1024 * 1024; // 1 MB = 1024 * 1024 bytes
        double megabytes = (double)bytes / megabyte;
        return RoundResultToTwoDecimalPlaces(megabytes);
    }

    static double RoundResultToTwoDecimalPlaces(double megabytes)
    {
        return Math.Round(megabytes, 2);
    }


    // Log block details
    static void LogFileBlockDetails(int chunkNumber, long blockOffset, int bytes, string sourceChunkHash)
    {
        double resultInMb = ConvertMegaBytesFromBytes(bytes);
        Log($"Chunk:{chunkNumber}  Position: {blockOffset}   Size: {resultInMb} MB, Hash: {sourceChunkHash}");
    }

    static string CreateBackupFile(string destinationPath)
    {
        string backupFilePath = destinationPath + ".backup";
        File.Copy(destinationPath, backupFilePath, overwrite: true);
        Log($"Backup created: {backupFilePath}");
        return backupFilePath;
    }

    static string ReadUserInputForSourceFilePath(string inputFileType)
    {
        string filePath;
        do
        {
            Console.Write($"Enter {inputFileType} file path: ");
            filePath = Console.ReadLine();
        } while (string.IsNullOrWhiteSpace(filePath));

        return filePath;
    }


    //Deleting partially transferred file
    static void DeletePartiallyTransferredFile(FileStream destinationStream, string destinationPath)
    {
        Log("Deleting partially transferred file...");
        destinationStream.Close(); // Close the stream before deleting the file
        File.Delete(destinationPath);
    }

    // Restore the original file from the backup
    static void RestoreOriginalFileFromBackup(FileStream destinationStream, string backupFilePath, string destinationPath)
    {
        // Restore the original file from the backup
        Log("Restoring the destination file from backup...");
        destinationStream.Close(); // Close the stream before restoring
        File.Copy(backupFilePath, destinationPath, overwrite: true);
    }


    // Verify the entire file after transfer
    static void VerifyFileIntegrity(string sourcePath, string destinationPath)
    {
        string sourceHash = ComputeSHA256ForFile(sourcePath);
        string destinationHash = ComputeSHA256ForFile(destinationPath);

        if (sourceHash == destinationHash)
        {
            Log("File integrity verified: Hashes match.");
        }
        else
        {
            Log("File integrity check failed! Hashes do not match.");
        }
    }


    static void CleanupBackupFile(string backupFilePath)
    {
        // Clean up the backup file if it exists
        if (backupFilePath != null && File.Exists(backupFilePath))
        {
            File.Delete(backupFilePath);
            Log($"Backup file deleted: {backupFilePath}");
        }
    }

    static async Task<bool> TransferBlockWithRetryAsync(FileStream destinationStream, byte[] buffer, int bytesRead, long blockOffset, int chunkNumber)
    {
        const int maxRetries = 5; // Maximum number of retries for a failed block

        // Compute MD5 hash at the source
        string sourceChunkHash = ComputeMD5Hash(buffer, bytesRead);

        LogFileBlockDetails(chunkNumber, blockOffset, bytesRead, sourceChunkHash);

        // Simulate corruption (only for the first attempt)
        byte[] possiblyCorruptedBuffer = SimulateCorruption(buffer, chunkNumber);

        int retryCount = 0;
        while (retryCount < maxRetries)
        {
            // Simulate corruption (for the rest of the attempts)
            if (retryCount > 0)
            {
                possiblyCorruptedBuffer = SimulateCorruption(buffer, chunkNumber);
            }


            // Write the block to the destination
            lock (destinationStream) // Ensure thread-safe access to the FileStream
            {
                destinationStream.Seek(blockOffset, SeekOrigin.Begin);
                destinationStream.Write(possiblyCorruptedBuffer, 0, bytesRead);
            }

            // Compute MD5 hash at the destination
            string destinationChunkHash = ComputeMD5Hash(possiblyCorruptedBuffer, bytesRead);

            if (sourceChunkHash == destinationChunkHash)
            {
                Console.WriteLine("Block verification successful.");
                return true;
            }

            // If hashes don't match, retry with exponential backoff
            retryCount++;
            Log($"Hash mismatch. Retry {retryCount}/{maxRetries}.");

            // Exponential backoff: Wait for (2^retryCount) seconds before retrying
            int delaySeconds = (int)Math.Pow(2, retryCount);
            await Task.Delay(delaySeconds * 1000);
        }

        // If it reaches here, it means the block failed after max retries
        return false;
    }

    static string ComputeMD5Hash(byte[] buffer, int count)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] hashBytes = md5.ComputeHash(buffer, 0, count);
            return ConvertByteArrayToHexString(hashBytes);
        }
    }

    static string ComputeSHA256ForFile(string filePath)
    {
        using (FileStream stream = File.OpenRead(filePath))
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(stream);
            return ConvertByteArrayToHexString(hash);
        }
    }

    static string ConvertByteArrayToHexString(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    static void Log(string message)
    {
        string logFilePath = "transfer_log.txt";
        using (StreamWriter writer = new StreamWriter(logFilePath, true))
        {
            writer.WriteLine($"{DateTime.Now}: {message}");
        }
        Console.WriteLine(message);
    }
}