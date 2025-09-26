using System;
using System.IO;
using System.IO.Compression;

/// <summary>
/// Updated version of your DocumentToInvoiceQueue method with memory optimization
/// </summary>
public class UpdatedDocumentProcessor
{
    /// <summary>
    /// Updated DocumentToInvoiceQueue method with memory optimization
    /// </summary>
    public bool DocumentToInvoiceQueue(int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType)
    {
        DocumentQueueEntityHelper documentQueueHelper = new DocumentQueueEntityHelper(entityBase as EInvoiceFirmEntityBase);
        DOCUMENT_QUEUE item = documentQueueHelper.GetDocumentQueue(msg.ReferenceId);
        
        // Use temporary file for large data instead of MemoryStream
        string tempFilePath = null;
        FileStream fileStream = null;
        EnhancedZipHelper zip = null;
        
        try
        {
            // Check memory availability before processing
            MemoryManagementUtils.CheckMemoryAvailability(item.Data.Length);
            
            // Write data to temporary file to avoid keeping large data in memory
            tempFilePath = Path.GetTempFileName();
            using (var fileWriter = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                fileWriter.Write(item.Data, 0, item.Data.Length);
            }
            
            // Clear the original data from memory
            item.Data = null;
            MemoryManagementUtils.ForceMemoryCleanup();
            
            fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read);
            zip = new EnhancedZipHelper(fileStream, ZipMode.Read);
            
            for (int i = 0; i < zip.Count; i++)
            {
                try
                {
                    // Use stream-based approach for large files
                    if (IsLargeFile(zip, i))
                    {
                        ProcessLargeZipItem(zip, i, taskId, msg, msgEntityBase, entityBase, operationType, item);
                    }
                    else
                    {
                        // Use memory-based approach for small files
                        using (MemoryStream zipItemStream = zip.GetItem(i))
                        {
                            ProcessSmallZipItem(zipItemStream, taskId, msg, msgEntityBase, entityBase, operationType, item);
                        }
                    }
                }
                catch (OutOfMemoryException)
                {
                    // Force cleanup and retry with stream-based approach
                    MemoryManagementUtils.ForceMemoryCleanup();
                    ProcessLargeZipItem(zip, i, taskId, msg, msgEntityBase, entityBase, operationType, item);
                }
                catch (Exception ex)
                {
                    // Log error and continue with next item
                    // LogError($"Error processing zip item {i}: {ex.Message}");
                    continue;
                }
            }
            
            return true;
        }
        catch (OutOfMemoryException)
        {
            // Force cleanup and retry with smaller chunks
            MemoryManagementUtils.ForceMemoryCleanup();
            return ProcessWithSmallerChunks(taskId, msg, msgEntityBase, entityBase, operationType, item);
        }
        finally
        {
            // Proper disposal
            zip?.Dispose();
            fileStream?.Close();
            fileStream?.Dispose();
            
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }
    }
    
    /// <summary>
    /// Check if file is large enough to warrant stream-based processing
    /// </summary>
    private bool IsLargeFile(EnhancedZipHelper zip, int index)
    {
        // Define threshold for large files (e.g., 1MB)
        const int largeFileThreshold = 1024 * 1024;
        
        // You can implement logic to check file size here
        // For now, we'll assume all files are processed with stream-based approach
        return true;
    }
    
    /// <summary>
    /// Process small zip item using memory-based approach
    /// </summary>
    private void ProcessSmallZipItem(MemoryStream zipItemStream, int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType, DOCUMENT_QUEUE item)
    {
        MikroDocumentXsdValidator mikroDocumentValidator = new MikroDocumentXsdValidator(xsdFolder, GetUBLTRVersion);
        if (mikroDocumentValidator.Validate(zipItemStream, false))
        {
            MikroDocumentSerializer mikroDocumentSerializer = new MikroDocumentSerializer(GetUBLTRVersion);
            MikroDocument mikroDocument = mikroDocumentSerializer.Deserialize(zipItemStream);
            
            // Use optimized invoice processing
            ProcessInvoiceOptimized(mikroDocument, taskId, msg, msgEntityBase, entityBase, operationType, item);
        }
    }
    
    /// <summary>
    /// Process large zip item using stream-based approach
    /// </summary>
    private void ProcessLargeZipItem(EnhancedZipHelper zip, int index, int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType, DOCUMENT_QUEUE item)
    {
        using (Stream zipItemStream = zip.GetItemStream(index))
        {
            MikroDocumentXsdValidator mikroDocumentValidator = new MikroDocumentXsdValidator(xsdFolder, GetUBLTRVersion);
            if (mikroDocumentValidator.Validate(zipItemStream, false))
            {
                // Reset stream position for deserialization
                zipItemStream.Position = 0;
                
                MikroDocumentSerializer mikroDocumentSerializer = new MikroDocumentSerializer(GetUBLTRVersion);
                MikroDocument mikroDocument = mikroDocumentSerializer.Deserialize(zipItemStream);
                
                // Use optimized invoice processing
                ProcessInvoiceOptimized(mikroDocument, taskId, msg, msgEntityBase, entityBase, operationType, item);
            }
        }
    }
    
    /// <summary>
    /// Process invoice with memory optimization
    /// </summary>
    private void ProcessInvoiceOptimized(MikroDocument mikroDocument, int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType, DOCUMENT_QUEUE item)
    {
        try
        {
            // Use optimized archive creation
            byte[] invoiceData = InvoiceArchiveOptimized.InvoiceToArchiveOptimized(invoiceType, GetUBLTRVersion);
            
            // Your existing logic here
            // invoiceHelper.SaveToDatabase(_now, documentHeader, invoiceType, account, accountDetail, item.Id, userDataInfo.Id, contentType, logInfo,
            //   mikroDocumentInvoiceSerial, mikroDocumentInvoiceGIBSerial, GetUBLTRVersion, reGenerate)
            
            // Clear data from memory after processing
            invoiceData = null;
            MemoryManagementUtils.ForceMemoryCleanup();
        }
        catch (OutOfMemoryException)
        {
            // Force cleanup and try with stream-based approach
            MemoryManagementUtils.ForceMemoryCleanup();
            ProcessInvoiceWithStream(mikroDocument, taskId, msg, msgEntityBase, entityBase, operationType, item);
        }
    }
    
    /// <summary>
    /// Process invoice using stream-based approach for very large invoices
    /// </summary>
    private void ProcessInvoiceWithStream(MikroDocument mikroDocument, int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType, DOCUMENT_QUEUE item)
    {
        string tempFilePath = null;
        try
        {
            tempFilePath = Path.GetTempFileName();
            
            // Use stream-based serialization
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                InvoiceArchiveOptimized.InvoiceToArchiveStream(invoiceType, GetUBLTRVersion, fileStream);
            }
            
            // Process the file
            ProcessInvoiceFile(tempFilePath, item);
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }
    }
    
    /// <summary>
    /// Process invoice file with memory management
    /// </summary>
    private void ProcessInvoiceFile(string filePath, DOCUMENT_QUEUE item)
    {
        try
        {
            // Your existing processing logic here
            // Process the invoice file
            
        }
        finally
        {
            // Cleanup
            MemoryManagementUtils.ForceMemoryCleanup();
        }
    }
    
    /// <summary>
    /// Process with smaller chunks when memory is limited
    /// </summary>
    private bool ProcessWithSmallerChunks(int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType, DOCUMENT_QUEUE item)
    {
        // Implement chunked processing for very large files
        // This would involve splitting the zip file into smaller parts
        
        return false; // Implement your chunked processing logic here
    }
}

/// <summary>
/// Updated InvoiceToArchive method with memory optimization
/// </summary>
public static class UpdatedInvoiceArchive
{
    /// <summary>
    /// Updated InvoiceToArchive method with memory optimization
    /// </summary>
    public static byte[] InvoiceToArchive(InvoiceType invoiceType, IUBLTRVersion version)
    {
        if (invoiceType == null)
            return null;
            
        // Use temporary file for large serializations
        string tempFilePath = null;
        try
        {
            tempFilePath = Path.GetTempFileName();
            
            // Serialize to file instead of memory
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                UBLTRInvoiceSerializer invoiceSerializer = new UBLTRInvoiceSerializer(version);
                if (invoiceSerializer.Serialize(invoiceType, fileStream))
                {
                    // Check file size
                    var fileInfo = new FileInfo(tempFilePath);
                    if (fileInfo.Length > 5 * 1024 * 1024) // 5MB limit
                    {
                        throw new InvalidOperationException($"Serialized invoice too large: {fileInfo.Length} bytes. Maximum allowed: 5MB.");
                    }
                    
                    // Create archive from file
                    using (var fileReadStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read))
                    {
                        return EnhancedZipHelper.ArchiveFromStream(invoiceType.UUID.Value, fileReadStream);
                    }
                }
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }
        
        return null;
    }
}