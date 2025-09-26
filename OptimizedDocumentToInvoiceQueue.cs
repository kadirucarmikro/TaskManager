using System;
using System.IO;
using System.IO.Compression;

/// <summary>
/// Optimize edilmiş DocumentToInvoiceQueue methodu
/// </summary>
public class OptimizedDocumentToInvoiceQueue
{
    /// <summary>
    /// Ana DocumentToInvoiceQueue methodunun optimize edilmiş versiyonu
    /// </summary>
    public bool DocumentToInvoiceQueue(int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType)
    {
        DocumentQueueEntityHelper documentQueueHelper = new DocumentQueueEntityHelper(entityBase as EInvoiceFirmEntityBase);
        DOCUMENT_QUEUE item = documentQueueHelper.GetDocumentQueue(msg.ReferenceId);
        
        // Dosya boyutuna göre yaklaşım seç
        const int largeFileThreshold = 50 * 1024 * 1024; // 50MB
        
        string tempFilePath = null;
        FileStream readerMem = null;
        MemoryStream memoryStream = null;
        ZipHelper zip = null;
        
        try
        {
            if (item.Data.Length > largeFileThreshold)
            {
                // Büyük dosyalar için FileStream kullan
                #region FileStream ve Zip İşlemleri
                tempFilePath = Path.GetTempFileName();
                using (var fileWriter = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
                {
                    fileWriter.Write(item.Data, 0, item.Data.Length);
                }
                
                // Original data'yı temizle
                item.Data = null;
                GC.Collect();
                
                readerMem = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read);
                zip = new ZipHelper(readerMem, ZipMode.Read);
                #endregion
            }
            else
            {
                // Küçük dosyalar için MemoryStream kullan
                #region MemoryStream ve Zip İşlemleri
                memoryStream = new MemoryStream(item.Data);
                zip = new ZipHelper(memoryStream, ZipMode.Read);
                #endregion
            }
            
            // Zip item'ları işle
            for (int i = 0; i < zip.Count; i++)
            {
                try
                {
                    ProcessZipItem(zip, i, taskId, msg, msgEntityBase, entityBase, operationType, item);
                }
                catch (OutOfMemoryException)
                {
                    // Memory yetersizse, stream-based approach kullan
                    GC.Collect();
                    ProcessZipItemAsStream(zip, i, taskId, msg, msgEntityBase, entityBase, operationType, item);
                }
                catch (Exception ex)
                {
                    // Log error ve devam et
                    // LogError($"Error processing zip item {i}: {ex.Message}");
                    continue;
                }
            }
            
            return true;
        }
        finally
        {
            // Proper cleanup
            zip?.Dispose();
            readerMem?.Close();
            readerMem?.Dispose();
            memoryStream?.Close();
            memoryStream?.Dispose();
            
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
        }
    }
    
    /// <summary>
    /// Zip item'ı memory-based olarak işle
    /// </summary>
    private void ProcessZipItem(ZipHelper zip, int index, int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType, DOCUMENT_QUEUE item)
    {
        using (MemoryStream zipItemStream = zip.GetItem(index))
        {
            MikroDocumentXsdValidator mikroDocumentValidator = new MikroDocumentXsdValidator(xsdFolder, GetUBLTRVersion);
            if (mikroDocumentValidator.Validate(zipItemStream, false))
            {
                MikroDocumentSerializer mikroDocumentSerializer = new MikroDocumentSerializer(GetUBLTRVersion);
                MikroDocument mikroDocument = mikroDocumentSerializer.Deserialize(zipItemStream);
                
                // Invoice'ı işle
                ProcessInvoice(mikroDocument, taskId, msg, msgEntityBase, entityBase, operationType, item);
            }
        }
    }
    
    /// <summary>
    /// Zip item'ı stream-based olarak işle (büyük dosyalar için)
    /// </summary>
    private void ProcessZipItemAsStream(ZipHelper zip, int index, int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType, DOCUMENT_QUEUE item)
    {
        using (Stream zipItemStream = zip.GetItemStream(index))
        {
            MikroDocumentXsdValidator mikroDocumentValidator = new MikroDocumentXsdValidator(xsdFolder, GetUBLTRVersion);
            if (mikroDocumentValidator.Validate(zipItemStream, false))
            {
                // Stream'i reset et
                zipItemStream.Position = 0;
                
                MikroDocumentSerializer mikroDocumentSerializer = new MikroDocumentSerializer(GetUBLTRVersion);
                MikroDocument mikroDocument = mikroDocumentSerializer.Deserialize(zipItemStream);
                
                // Invoice'ı işle
                ProcessInvoice(mikroDocument, taskId, msg, msgEntityBase, entityBase, operationType, item);
            }
        }
    }
    
    /// <summary>
    /// Invoice'ı işle
    /// </summary>
    private void ProcessInvoice(MikroDocument mikroDocument, int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType, DOCUMENT_QUEUE item)
    {
        try
        {
            // Optimize edilmiş archive oluştur
            byte[] invoiceData = InvoiceToArchiveOptimized(invoiceType, GetUBLTRVersion);
            
            // Mevcut logic'inizi buraya ekleyin
            // invoiceHelper.SaveToDatabase(_now, documentHeader, invoiceType, account, accountDetail, item.Id, userDataInfo.Id, contentType, logInfo,
            //   mikroDocumentInvoiceSerial, mikroDocumentInvoiceGIBSerial, GetUBLTRVersion, reGenerate)
            
            // Memory temizle
            invoiceData = null;
            GC.Collect();
        }
        catch (OutOfMemoryException)
        {
            // Stream-based approach kullan
            ProcessInvoiceAsStream(mikroDocument, taskId, msg, msgEntityBase, entityBase, operationType, item);
        }
    }
    
    /// <summary>
    /// Invoice'ı stream-based olarak işle
    /// </summary>
    private void ProcessInvoiceAsStream(MikroDocument mikroDocument, int taskId, PROCESS_MESSAGE msg, EInvoiceEntityBase msgEntityBase, object entityBase, ProcessOperationType operationType, DOCUMENT_QUEUE item)
    {
        string tempFilePath = null;
        try
        {
            tempFilePath = Path.GetTempFileName();
            
            // Stream-based serialization
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                UBLTRInvoiceSerializer invoiceSerializer = new UBLTRInvoiceSerializer(GetUBLTRVersion);
                invoiceSerializer.Serialize(invoiceType, fileStream);
            }
            
            // File'ı işle
            ProcessInvoiceFile(tempFilePath, item);
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
        }
    }
    
    /// <summary>
    /// Invoice file'ı işle
    /// </summary>
    private void ProcessInvoiceFile(string filePath, DOCUMENT_QUEUE item)
    {
        try
        {
            // File processing logic buraya
        }
        finally
        {
            GC.Collect();
        }
    }
    
    /// <summary>
    /// Optimize edilmiş InvoiceToArchive methodu
    /// </summary>
    public static byte[] InvoiceToArchiveOptimized(InvoiceType invoiceType, IUBLTRVersion version)
    {
        if (invoiceType == null)
            return null;
            
        // Temporary file kullan
        string tempFilePath = null;
        try
        {
            tempFilePath = Path.GetTempFileName();
            
            // File'a serialize et
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                UBLTRInvoiceSerializer invoiceSerializer = new UBLTRInvoiceSerializer(version);
                if (invoiceSerializer.Serialize(invoiceType, fileStream))
                {
                    // File size check
                    var fileInfo = new FileInfo(tempFilePath);
                    if (fileInfo.Length > 5 * 1024 * 1024) // 5MB limit
                    {
                        return null; // Çok büyük, null döndür
                    }
                    
                    // Archive oluştur
                    using (var fileReadStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read))
                    {
                        return ZipHelper.ArchiveFromStream(invoiceType.UUID.Value, fileReadStream);
                    }
                }
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
        }
        
        return null;
    }
}

/// <summary>
/// Optimize edilmiş ZipHelper
/// </summary>
public class OptimizedZipHelper : IDisposable
{
    private ZipArchive _zip;
    private bool _disposed = false;
    
    public OptimizedZipHelper(Stream stream, ZipMode zipMode)
    {
        _zip = new ZipArchive(stream, zipMode == Helper.ZipMode.Create ? ZipArchiveMode.Create : ZipArchiveMode.Read, true);
    }
    
    public int Count => _zip.Entries.Count;
    
    /// <summary>
    /// Item'ı MemoryStream olarak al (küçük dosyalar için)
    /// </summary>
    public MemoryStream GetItem(int index, int maxSizeBytes = 1024 * 1024) // 1MB limit
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OptimizedZipHelper));
            
        var entry = _zip.Entries[index];
        
        // Size check
        if (entry.Length > maxSizeBytes)
        {
            throw new InvalidOperationException($"File too large: {entry.Length} bytes. Use GetItemStream instead.");
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
    /// Item'ı Stream olarak al (büyük dosyalar için)
    /// </summary>
    public Stream GetItemStream(int index)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OptimizedZipHelper));
            
        return _zip.Entries[index].Open();
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