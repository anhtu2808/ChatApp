let connection = null;
let isConnected = false;
let typingTimeout = null;

const connectBtn = document.getElementById('connectBtn');
const disconnectBtn = document.getElementById('disconnectBtn');
const usernameInput = document.getElementById('username');
const serverUrlInput = document.getElementById('serverUrl');
const messagesDiv = document.getElementById('messages');
const messageBox = document.getElementById('messageBox');
const typingStatus = document.getElementById('typingStatus');
const progressBar = document.getElementById('progressBar');
const onlineUsersDiv = document.getElementById('onlineUsers');

connectBtn.onclick = async () => {
    if (isConnected) {
        alert('Already connected.');
        return;
    }
    const user = usernameInput.value.trim();
    const ip = serverUrlInput.value.trim();
    const serverUrl = `http://${ip}:5262`;
    if (!user || !ip) {
        alert('Please enter username and server URL.');
        return;
    }
    connection = new signalR.HubConnectionBuilder()
        .withUrl(`${serverUrl}/chathub?username=${encodeURIComponent(user)}`)
        .withAutomaticReconnect()
        .build();

    connection.on('ReceiveMessage', (user, message) => {
        if (message.startsWith('[image]'))
            addImage(user, message.substring(7));
        else if (message.startsWith('[file]'))
            addFileLink(user, message.substring(6));
        else
            addMessage(user, message);
    });

    connection.on('UserTyping', user => {
        typingStatus.textContent = `${user} is typing...`;
    });

    connection.on('UserStoppedTyping', _ => {
        typingStatus.textContent = '';
    });

    connection.on('UpdateUserList', users => {
        onlineUsersDiv.innerHTML = '';
        users.sort((a, b) => b.isOnline - a.isOnline || a.username.localeCompare(b.username));
        users.forEach(u => {
            const div = document.createElement('div');
            div.textContent = `${u.isOnline ? '🟢' : '⚪'} ${u.username}`;
            onlineUsersDiv.appendChild(div);
        });
    });

    try {
        await connection.start();
        await connection.invoke('Register', user);
        isConnected = true;
        usernameInput.disabled = true;
        serverUrlInput.disabled = true;
        alert('Connected');
    } catch (err) {
        alert('Connection failed: ' + err);
    }
};

disconnectBtn.onclick = async () => {
    if (connection && isConnected) {
        try { await connection.stop(); } catch(e) { console.error(e); }
    }
    isConnected = false;
    connection = null;
    messagesDiv.innerHTML = '';
    onlineUsersDiv.innerHTML = '';
    typingStatus.textContent = '';
    progressBar.value = 0;
    usernameInput.disabled = false;
    serverUrlInput.disabled = false;
    alert('Disconnected');
};

document.getElementById('sendBtn').onclick = async () => {
    if (!isConnected) {
        alert('Connect first');
        return;
    }
    const user = usernameInput.value.trim();
    const message = messageBox.value.trim();
    if (user && message) {
        await connection.invoke('SendMessage', user, message);
        messageBox.value = '';
    }
};

messageBox.addEventListener('input', async () => {
    if (!isConnected) return;
    await connection.invoke('Typing', usernameInput.value.trim());
    if (typingTimeout) clearTimeout(typingTimeout);
    typingTimeout = setTimeout(() => {
        connection.invoke('StopTyping', usernameInput.value.trim());
    }, 1500);
});

document.getElementById('imageBtn').onclick = () => {
    document.getElementById('imageInput').click();
};

document.getElementById('fileBtn').onclick = () => {
    document.getElementById('fileInput').click();
};

document.getElementById('imageInput').onchange = async (e) => {
    if (!isConnected) return;
    const file = e.target.files[0];
    if (!file) return;
    const ip = serverUrlInput.value.trim();
    const url = `http://${ip}:5262/api/imageupload`;
    const form = new FormData();
    form.append('file', file);
    try {
        const resp = await fetch(url, { method: 'POST', body: form });
        if (resp.ok) {
            const data = await resp.json();
            await connection.invoke('SendMessage', usernameInput.value.trim(), '[image]' + data.url);
        } else {
            alert('Image upload failed');
        }
    } catch (err) {
        alert('Error: ' + err);
    }
    e.target.value = '';
};

document.getElementById('fileInput').onchange = async (e) => {
    if (!isConnected) return;
    const file = e.target.files[0];
    if (!file) return;
    const ip = serverUrlInput.value.trim();
    const url = `http://${ip}:5262/api/fileupload`;
    const form = new FormData();
    form.append('file', file);
    progressBar.value = 0;
    try {
        const respText = await uploadWithProgress(url, form, p => progressBar.value = p);
        progressBar.value = 0;
        const data = JSON.parse(respText);
        await connection.invoke('SendMessage', usernameInput.value.trim(), '[file]' + data.url);
    } catch (err) {
        alert('File upload failed');
    }
    e.target.value = '';
};

async function uploadWithProgress(url, form, onProgress) {
    const xhr = new XMLHttpRequest();
    const promise = new Promise((resolve, reject) => {
        xhr.open('POST', url, true);
        xhr.upload.onprogress = e => {
            if (e.lengthComputable) {
                const percent = (e.loaded / e.total) * 100;
                onProgress(percent);
            }
        };
        xhr.onload = () => {
            if (xhr.status >= 200 && xhr.status < 300) resolve(xhr.response);
            else reject(xhr.statusText);
        };
        xhr.onerror = () => reject('Network error');
    });
    xhr.send(form);
    return promise;
}

function addMessage(user, msg) {
    const div = document.createElement('div');
    div.classList.add('border', 'rounded', 'p-2', 'mb-1');
    div.style.backgroundColor = user === usernameInput.value.trim() ? '#cce5ff' : '#eeeeee';
    if (user !== usernameInput.value.trim()) {
        const name = document.createElement('div');
        name.style.fontWeight = 'bold';
        name.classList.add('text-muted');
        name.innerText = user;
        div.appendChild(name);
    }
    const text = document.createElement('div');
    text.innerText = msg;
    div.appendChild(text);
    messagesDiv.appendChild(div);
    messagesDiv.scrollTop = messagesDiv.scrollHeight;
}

function addImage(user, url) {
    const div = document.createElement('div');
    div.classList.add('border', 'rounded', 'p-2', 'mb-1');
    div.style.backgroundColor = user === usernameInput.value.trim() ? '#cce5ff' : '#eeeeee';
    if (user !== usernameInput.value.trim()) {
        const name = document.createElement('div');
        name.style.fontWeight = 'bold';
        name.classList.add('text-muted');
        name.innerText = user;
        div.appendChild(name);
    }
    const img = document.createElement('img');
    img.src = url;
    img.style.width = '200px';
    img.style.height = '150px';
    img.style.objectFit = 'cover';
    div.appendChild(img);
    messagesDiv.appendChild(div);
    messagesDiv.scrollTop = messagesDiv.scrollHeight;
}

function addFileLink(user, url) {
    const div = document.createElement('div');
    div.classList.add('border', 'rounded', 'p-2', 'mb-1');
    div.style.backgroundColor = user === usernameInput.value.trim() ? '#cce5ff' : '#eeeeee';
    if (user !== usernameInput.value.trim()) {
        const name = document.createElement('div');
        name.style.fontWeight = 'bold';
        name.classList.add('text-muted');
        name.innerText = user;
        div.appendChild(name);
    }
    const a = document.createElement('a');
    a.href = url;
    a.textContent = '📎 ' + url.split('/').pop();
    a.download = '';
    div.appendChild(a);
    messagesDiv.appendChild(div);
    messagesDiv.scrollTop = messagesDiv.scrollHeight;
}
