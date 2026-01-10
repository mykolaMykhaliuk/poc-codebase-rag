// Admin UI JavaScript helpers

// Download file from base64 content - more reliable for Blazor Server
window.downloadFileFromBase64 = function (filename, base64Content, contentType) {
    try {
        // Decode base64 to binary
        const byteCharacters = atob(base64Content);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: contentType || 'application/octet-stream' });

        // Create download link
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        link.style.display = 'none';
        document.body.appendChild(link);

        // Trigger download
        link.click();

        // Cleanup after a short delay to ensure download starts
        setTimeout(function() {
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        }, 100);
    } catch (error) {
        console.error('Download failed:', error);
        throw error;
    }
};

// Legacy download file function (kept for backwards compatibility)
window.downloadFile = function (filename, content) {
    try {
        const blob = new Blob([content], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        link.style.display = 'none';
        document.body.appendChild(link);
        link.click();

        setTimeout(function() {
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        }, 100);
    } catch (error) {
        console.error('Download failed:', error);
        throw error;
    }
};
