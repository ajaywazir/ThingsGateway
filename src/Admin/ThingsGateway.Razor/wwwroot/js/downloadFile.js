//下载文件
export async function blazor_downloadFile(url, fileName, dtoObject) {
    const params = new URLSearchParams();

    // 将 dtoObject 的属性添加到 URLSearchParams 中
    for (const key in dtoObject) {
        if (dtoObject[key] !== null) {
            params.append(key, dtoObject[key]);
        }
    }

    // 构建完整的 URL
    const fullUrl = `${url}?${params.toString()}`;

    try {
        // 发起 fetch 请求
        const response = await fetch(fullUrl);

        // 获取响应头中的 content-disposition
        const dispositionHeader = response.headers.get('content-disposition');
        let resolvedFileName = fileName;

        if (dispositionHeader) {
            // 解析出文件名
            const match = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/.exec(dispositionHeader);
            const serverFileName = match && match[1] ? match[1].replace(/['"]/g, '') : null;
            if (serverFileName) {
                resolvedFileName = serverFileName;
            }
        }

        // 将响应转换为 blob 对象
        const blob = await response.blob();

        // 创建临时的文件 URL
        const fileUrl = window.URL.createObjectURL(blob);

        // 创建一个 <a> 元素并设置下载链接和文件名
        const anchorElement = document.createElement('a');
        anchorElement.href = fileUrl;
        anchorElement.download = resolvedFileName;
        anchorElement.style.display = 'none';

        // 将 <a> 元素添加到 body 中并触发下载
        document.body.appendChild(anchorElement);
        anchorElement.click();
        document.body.removeChild(anchorElement);

        // 撤销临时的文件 URL
        window.URL.revokeObjectURL(fileUrl);

        return true;
    } catch (error) {
        console.error('DownFile error ', error);
        throw error;
    }
}




//下载文件
export async function postJson_downloadFile(url, fileName, jsonBody) {

    try {
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: jsonBody
        });

        const dispositionHeader = response.headers.get('content-disposition');
        let resolvedFileName = fileName;

        if (dispositionHeader) {
            const match = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/.exec(dispositionHeader);
            const serverFileName = match && match[1] ? match[1].replace(/['"]/g, '') : null;
            if (serverFileName) {
                resolvedFileName = serverFileName;
            }
        }

        const blob = await response.blob();
        const fileUrl = window.URL.createObjectURL(blob);

        const anchorElement = document.createElement('a');
        anchorElement.href = fileUrl;
        anchorElement.download = resolvedFileName;
        anchorElement.style.display = 'none';

        document.body.appendChild(anchorElement);
        anchorElement.click();
        document.body.removeChild(anchorElement);

        window.URL.revokeObjectURL(fileUrl);

        return true; // 唯一新增的返回值
    } catch (error) {
        console.error('downfile error ', error);
    }
}
