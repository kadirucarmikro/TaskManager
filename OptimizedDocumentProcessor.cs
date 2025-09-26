using System;
using System.IO;
using System.IO.Compression;

/// <summary>
/// Optimized version of your existing DocumentToInvoiceQueue method
/// </summary>
public class OptimizedDocumentProcessor
{
    /// <summary>
    /// Memory-optimized version of DocumentToInvoiceQueue
    /// </summary>
    public bool DocumentToInvoiceQueueOptimized(int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType)
    {
        DocumentQueueEntityHelper documentQueueHelper = new DocumentQueueEntityHelper(entityBase as EInvoiceFirmEntityBase);
        DOCUMENT_QUEUE item = documentQueueHelper.GetDocumentQueue(msg.ReferenceId);
        
        // Use temporary file for large data instead of MemoryStream
        string tempFilePath = null;
        FileStream fileStream = null;
        MemoryOptimizedZipHelper zip = null;
        
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
            zip = new MemoryOptimizedZipHelper(fileStream, ZipMode.Read);
            
            for (int i = 0; i < zip.Count; i++)
            {
                try
                {
                    // Process each zip item using stream-based approach
                    using (Stream zipItemStream = zip.GetItemStreamOptimized(i))
                    {
                        if (ProcessZipItemOptimized(zipItemStream, taskId, msg, msgEntityBase, entityBase, operationType, item))
                        {
                            // Item processed successfully
                        }
                    }
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
    /// Process individual zip item with memory optimization
    /// </summary>
    private bool ProcessZipItemOptimized(Stream zipItemStream, int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType, DOCUMENT_QUEUE item)
    {
        try
        {
            MikroDocumentXsdValidator mikroDocumentValidator = new MikroDocumentXsdValidator(xsdFolder, GetUBLTRVersion);
            if (mikroDocumentValidator.Validate(zipItemStream, false))
            {
                // Reset stream position for deserialization
                zipItemStream.Position = 0;
                
                MikroDocumentSerializer mikroDocumentSerializer = new MikroDocumentSerializer(GetUBLTRVersion);
                MikroDocument mikroDocument = mikroDocumentSerializer.Deserialize(zipItemStream);
                
                // Use optimized invoice processing
                return ProcessInvoiceOptimized(mikroDocument, taskId, msg, msgEntityBase, entityBase, operationType, item);
            }
        }
        catch (Exception ex)
        {
            // Log error
            // LogError($"Error processing zip item: {ex.Message}");
            return false;
        }
        
        return false;
    }
    
    /// <summary>
    /// Process invoice with memory optimization
    /// </summary>
    private bool ProcessInvoiceOptimized(MikroDocument mikroDocument, int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType, DOCUMENT_QUEUE item)
    {
        try
        {
            // Your existing logic here, but use optimized methods
            // invoiceHelper.SaveToDatabase(_now, documentHeader, invoiceType, account, accountDetail, item.Id, userDataInfo.Id, contentType, logInfo,
            //   mikroDocumentInvoiceSerial, mikroDocumentInvoiceGIBSerial, GetUBLTRVersion, reGenerate)
            
            // Use optimized archive creation
            byte[] invoiceData = InvoiceArchiveOptimized.InvoiceToArchiveOptimized(invoiceType, GetUBLTRVersion);
            
            // Process the invoice data
            return ProcessInvoiceData(invoiceData, item);
        }
        catch (OutOfMemoryException)
        {
            // Force cleanup and try with stream-based approach
            MemoryManagementUtils.ForceMemoryCleanup();
            return ProcessInvoiceWithStream(mikroDocument, taskId, msg, msgEntityBase, entityBase, operationType, item);
        }
    }
    
    /// <summary>
    /// Process invoice using stream-based approach for very large invoices
    /// </summary>
    private bool ProcessInvoiceWithStream(MikroDocument mikroDocument, int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType, DOCUMENT_QUEUE item)
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
            return ProcessInvoiceFile(tempFilePath, item);
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
    /// Process invoice data with memory management
    /// </summary>
    private bool ProcessInvoiceData(byte[] invoiceData, DOCUMENT_QUEUE item)
    {
        if (invoiceData == null)
            return false;
            
        try
        {
            // Your existing processing logic here
            // Process the invoice data
            
            return true;
        }
        finally
        {
            // Clear data from memory
            invoiceData = null;
            MemoryManagementUtils.ForceMemoryCleanup();
        }
    }
    
    /// <summary>
    /// Process invoice file with memory management
    /// </summary>
    private bool ProcessInvoiceFile(string filePath, DOCUMENT_QUEUE item)
    {
        try
        {
            // Your existing processing logic here
            // Process the invoice file
            
            return true;
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
/// Enhanced ZipHelper with better memory management
/// </summary>
public class EnhancedZipHelper : IDisposable
{
    private ZipArchive _zip;
    private bool _disposed = false;
    
    public EnhancedZipHelper(Stream stream, ZipMode zipMode)
    {
        _zip = new ZipArchive(stream, zipMode == Helper.ZipMode.Create ? ZipArchiveMode.Create : ZipArchiveMode.Read, true);
    }
    
    public int Count => _zip.Entries.Count;
    
    /// <summary>
    /// Get item as stream with memory optimization
    /// </summary>
    public Stream GetItemStream(int index)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EnhancedZipHelper));
            
        return _zip.Entries[index].Open();
    }
    
    /// <summary>
    /// Get item as MemoryStream with size limit
    /// </summary>
    public MemoryStream GetItem(int index, int maxSizeBytes = 1024 * 1024) // 1MB default limit
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EnhancedZipHelper));
            
        var entry = _zip.Entries[index];
        
        // Check size before loading to memory
        if (entry.Length > maxSizeBytes)
        {
            throw new InvalidOperationException($"File too large: {entry.Length} bytes. Maximum allowed: {maxSizeBytes} bytes. Use GetItemStream instead.");
        }
        
        MemoryStream ret = new MemoryStream();
        using (Stream dataStream = entry.Open())
        {
            dataStream.CopyTo(ret);
            ret.Position = 0;
        }
        return ret;
    }
    
    /// <summary>
    /// Process large file in chunks
    /// </summary>
    public void ProcessLargeFile(int index, Action<byte[], int, int> chunkProcessor, int chunkSize = 8192)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EnhancedZipHelper));
            
        var entry = _zip.Entries[index];
        byte[] buffer = new byte[chunkSize];
        
        using (Stream dataStream = entry.Open())
        {
            int bytesRead;
            while ((bytesRead = dataStream.Read(buffer, 0, chunkSize)) > 0)
            {
                chunkProcessor(buffer, 0, bytesRead);
            }
        }
    }
    
    /// <summary>
    /// Create archive with memory optimization
    /// </summary>
    public static byte[] ArchiveFromStream(string fileName, Stream data, int maxSizeBytes = 10 * 1024 * 1024) // 10MB limit
    {
        if (data.Length > maxSizeBytes)
        {
            throw new InvalidOperationException($"Data too large: {data.Length} bytes. Maximum allowed: {maxSizeBytes} bytes.");
        }
        
        return ArchiveFromStream(fileName, null, data);
    }
    
    public static byte[] ArchiveFromStream(string fileName, string fileExtension, Stream data)
    {
        byte[] ret = null;
        MemoryStream zipData = new MemoryStream();
        
        try
        {
            using (var zip = new EnhancedZipHelper(zipData, ZipMode.Create))
            {
                if (string.IsNullOrWhiteSpace(fileExtension)) 
                    fileExtension = ".xml";
                    
                zip.AddFromStream(string.Format("{0}{1}", Path.GetFileNameWithoutExtension(fileName), fileExtension), data);
            }
            
            if (zipData.Length > 0)
                ret = zipData.ToArray();
        }
        finally
        {
            zipData?.Close();
            zipData?.Dispose();
        }
        
        return ret;
    }
    
    public void AddFromStream(string fileName, Stream data)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EnhancedZipHelper));
            
        ZipArchiveEntry entry = _zip.CreateEntry(fileName);
        using (Stream zipStream = entry.Open())
        {
            data.Position = 0;
            data.CopyTo(zipStream);
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _zip?.Dispose();
            _disposed = true;
        }
    }
}