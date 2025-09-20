// Function to trigger file download
window.downloadFile = function (base64Data, fileName, contentType) {
    try {
        // Convert base64 to blob
        const byteCharacters = atob(base64Data);
        const byteArrays = [];

        for (let offset = 0; offset < byteCharacters.length; offset += 1024) {
            const slice = byteCharacters.slice(offset, offset + 1024);

            const byteNumbers = new Array(slice.length);
            for (let i = 0; i < slice.length; i++) {
                byteNumbers[i] = slice.charCodeAt(i);
            }

            const byteArray = new Uint8Array(byteNumbers);
            byteArrays.push(byteArray);
        }

        const blob = new Blob(byteArrays, { type: contentType });
        const url = URL.createObjectURL(blob);

        // Create download link and trigger click
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();

        // Clean up
        setTimeout(function () {
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        }, 100);

        return true;
    } catch (ex) {
        console.error('Download error:', ex);
        return false;
    }
};