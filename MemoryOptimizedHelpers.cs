using System;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Xml.Serialization;

/// <summary>
/// Memory-efficient helper methods to prevent OutOfMemoryException
/// </summary>
public static class MemoryOptimizedHelpers
{
    /// <summary>
    /// Memory-efficient version of DocumentToInvoiceQueue that processes files in chunks
    /// </summary>
    public static bool DocumentToInvoiceQueueOptimized(int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType)
    {
        DocumentQueueEntityHelper documentQueueHelper = new DocumentQueueEntityHelper(entityBase as EInvoiceFirmEntityBase);
        DOCUMENT_QUEUE item = documentQueueHelper.GetDocumentQueue(msg.ReferenceId);
        
        // Use FileStream instead of MemoryStream for large files
        string tempFilePath = null;
        FileStream fileStream = null;
        ZipHelper zip = null;
        
        try
        {
            // Write to temporary file instead of keeping in memory
            tempFilePath = Path.GetTempFileName();
            using (var fileWriter = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                fileWriter.Write(item.Data, 0, item.Data.Length);
            }
            
            fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read);
            zip = new ZipHelper(fileStream, ZipMode.Read);
            
            for (int i = 0; i < zip.Count; i++)
            {
                // Use stream-based processing instead of loading entire file to memory
                using (Stream zipItemStream = zip.GetItemStreamOptimized(i))
                {
                    if (ProcessZipItemStream(zipItemStream, taskId, msg, msgEntityBase, entityBase, operationType))
                    {
                        // Process successful
                    }
                }
            }
            
            return true;
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
    /// Process individual zip item stream without loading to memory
    /// </summary>
    private static bool ProcessZipItemStream(Stream zipItemStream, int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType)
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
                
                // Process the document
                return ProcessMikroDocument(mikroDocument, taskId, msg, msgEntityBase, entityBase, operationType);
            }
        }
        catch (Exception ex)
        {
            // Log error and continue processing other items
            // LogError($"Error processing zip item: {ex.Message}");
            return false;
        }
        
        return false;
    }
    
    /// <summary>
    /// Process MikroDocument without loading to memory
    /// </summary>
    private static bool ProcessMikroDocument(MikroDocument mikroDocument, int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType)
    {
        // Your existing logic here
        // This is where you would call invoiceHelper.SaveToDatabase with optimized parameters
        return true;
    }
}

/// <summary>
/// Memory-optimized ZipHelper with streaming support
/// </summary>
public class MemoryOptimizedZipHelper : IDisposable
{
    private ZipArchive _zip;
    private bool _disposed = false;
    
    public MemoryOptimizedZipHelper(Stream stream, ZipMode zipMode)
    {
        _zip = new ZipArchive(stream, zipMode == Helper.ZipMode.Create ? ZipArchiveMode.Create : ZipArchiveMode.Read, true);
    }
    
    public int Count => _zip.Entries.Count;
    
    /// <summary>
    /// Get item as stream without loading to memory
    /// </summary>
    public Stream GetItemStreamOptimized(int index)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MemoryOptimizedZipHelper));
            
        return _zip.Entries[index].Open();
    }
    
    /// <summary>
    /// Get item as MemoryStream (use only for small files)
    /// </summary>
    public MemoryStream GetItemAsMemoryStream(int index, int maxSizeBytes = 1024 * 1024) // 1MB limit
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MemoryOptimizedZipHelper));
            
        var entry = _zip.Entries[index];
        
        // Check if file is too large
        if (entry.Length > maxSizeBytes)
        {
            throw new InvalidOperationException($"File too large: {entry.Length} bytes. Maximum allowed: {maxSizeBytes} bytes.");
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
    /// Process large file in chunks to avoid memory issues
    /// </summary>
    public void ProcessLargeFileInChunks(int index, Action<byte[], int, int> chunkProcessor, int chunkSize = 8192)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MemoryOptimizedZipHelper));
            
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
    /// Create archive from stream with memory optimization
    /// </summary>
    public static byte[] ArchiveFromStreamOptimized(string fileName, Stream data, int maxSizeBytes = 10 * 1024 * 1024) // 10MB limit
    {
        if (data.Length > maxSizeBytes)
        {
            throw new InvalidOperationException($"Data too large: {data.Length} bytes. Maximum allowed: {maxSizeBytes} bytes.");
        }
        
        return ArchiveFromStreamOptimized(fileName, null, data);
    }
    
    public static byte[] ArchiveFromStreamOptimized(string fileName, string fileExtension, Stream data)
    {
        byte[] ret = null;
        MemoryStream zipData = new MemoryStream();
        
        try
        {
            using (var zip = new MemoryOptimizedZipHelper(zipData, ZipMode.Create))
            {
                if (string.IsNullOrWhiteSpace(fileExtension)) 
                    fileExtension = ".xml";
                    
                zip.AddFromStreamOptimized(string.Format("{0}{1}", Path.GetFileNameWithoutExtension(fileName), fileExtension), data);
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
    
    public void AddFromStreamOptimized(string fileName, Stream data)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MemoryOptimizedZipHelper));
            
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

/// <summary>
/// Memory-optimized invoice serialization
/// </summary>
public static class InvoiceArchiveOptimized
{
    /// <summary>
    /// Create invoice archive with memory optimization
    /// </summary>
    public static byte[] InvoiceToArchiveOptimized(InvoiceType invoiceType, IUBLTRVersion version, int maxSizeBytes = 5 * 1024 * 1024) // 5MB limit
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
                    if (fileInfo.Length > maxSizeBytes)
                    {
                        throw new InvalidOperationException($"Serialized invoice too large: {fileInfo.Length} bytes. Maximum allowed: {maxSizeBytes} bytes.");
                    }
                    
                    // Create archive from file
                    using (var fileReadStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read))
                    {
                        return MemoryOptimizedZipHelper.ArchiveFromStreamOptimized(invoiceType.UUID.Value, fileReadStream);
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
    
    /// <summary>
    /// Stream-based invoice serialization for very large invoices
    /// </summary>
    public static void InvoiceToArchiveStream(InvoiceType invoiceType, IUBLTRVersion version, Stream outputStream)
    {
        if (invoiceType == null || outputStream == null)
            return;
            
        UBLTRInvoiceSerializer invoiceSerializer = new UBLTRInvoiceSerializer(version);
        invoiceSerializer.Serialize(invoiceType, outputStream);
    }
}

/// <summary>
/// Memory-optimized XML serializer
/// </summary>
public class MemoryOptimizedUBLTRInvoiceSerializer : UBLTRInvoiceSerializer
{
    /// <summary>
    /// Serialize with memory optimization
    /// </summary>
    public bool SerializeOptimized(InvoiceType invoiceType, Stream outputStream, int maxMemoryUsage = 10 * 1024 * 1024) // 10MB limit
    {
        if (outputStream == null)
            return false;
            
        // Check if we should use file-based serialization
        if (ShouldUseFileBasedSerialization(invoiceType))
        {
            return SerializeToFile(invoiceType, outputStream);
        }
        
        // Use regular serialization for small invoices
        return Serialize(invoiceType, outputStream);
    }
    
    private bool ShouldUseFileBasedSerialization(InvoiceType invoiceType)
    {
        // Add logic to determine if invoice is large enough to warrant file-based serialization
        // This could be based on line item count, invoice amount, etc.
        return false; // Implement your logic here
    }
    
    private bool SerializeToFile(InvoiceType invoiceType, Stream outputStream)
    {
        string tempFilePath = null;
        try
        {
            tempFilePath = Path.GetTempFileName();
            
            // Serialize to temporary file
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                if (Serialize(invoiceType, fileStream))
                {
                    // Copy from file to output stream
                    using (var fileReadStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read))
                    {
                        fileReadStream.CopyTo(outputStream);
                        return true;
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
        
        return false;
    }
}

/// <summary>
/// Utility class for memory management
/// </summary>
public static class MemoryManagementUtils
{
    /// <summary>
    /// Force garbage collection and memory cleanup
    /// </summary>
    public static void ForceMemoryCleanup()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
    
    /// <summary>
    /// Check available memory and throw exception if insufficient
    /// </summary>
    public static void CheckMemoryAvailability(long requiredBytes)
    {
        var availableMemory = GC.GetTotalMemory(false);
        var maxMemory = GC.GetTotalMemory(true);
        
        if (availableMemory + requiredBytes > maxMemory * 0.8) // Use 80% of available memory as threshold
        {
            throw new OutOfMemoryException($"Insufficient memory. Required: {requiredBytes} bytes, Available: {availableMemory} bytes");
        }
    }
    
    /// <summary>
    /// Get current memory usage information
    /// </summary>
    public static string GetMemoryInfo()
    {
        var totalMemory = GC.GetTotalMemory(false);
        var maxMemory = GC.GetTotalMemory(true);
        return $"Memory: {totalMemory / 1024 / 1024}MB / {maxMemory / 1024 / 1024}MB";
    }
}