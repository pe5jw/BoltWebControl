// BoltAudioBridge frontend
// WebRTC signaling via SignalR → SIPSorcery server-side peer → miniaudio soundcard

const hub = new signalR.HubConnectionBuilder()
    .withUrl('/hub/audio')
    .withAutomaticReconnect()
    .build();

let pc = null;

async function init() {
    // Enumerate browser audio inputs
    const devices = await navigator.mediaDevices.enumerateDevices();
    const inputs = devices.filter(d => d.kind === 'audioinput');
    const sel = document.getElementById('browserInput');
    sel.innerHTML = inputs.map(d =>
        `<option value="${d.deviceId}">${d.label || d.deviceId}</option>`
    ).join('');

    // Haal server devices op
    const res = await fetch('/api/audio/devices');
    const data = await res.json();

    const srvIn  = document.getElementById('serverInput');
    const srvOut = document.getElementById('serverOutput');
    srvIn.innerHTML  = data.inputs.map(d =>
        `<option value="${d.id}">${d.name}${d.isDefault?' (default)':''}</option>`).join('');
    srvOut.innerHTML = data.outputs.map(d =>
        `<option value="${d.id}">${d.name}${d.isDefault?' (default)':''}</option>`).join('');

    setStatus('Devices geladen.');
}

hub.on('IceCandidate', (candidate, sdpMid, sdpMLineIndex) => {
    if (pc) pc.addIceCandidate({ candidate, sdpMid, sdpMLineIndex });
});

document.getElementById('btnConnect').onclick = async () => {
    setStatus('Verbinden...');
    await hub.start();

    const deviceId = document.getElementById('browserInput').value;
    const stream = await navigator.mediaDevices.getUserMedia({
        audio: { deviceId: { exact: deviceId } }
    });

    pc = new RTCPeerConnection({ iceServers: [] });
    stream.getTracks().forEach(t => pc.addTrack(t, stream));

    pc.ontrack = e => {
        const audio = new Audio();
        audio.srcObject = e.streams[0];
        audio.play();
    };

    pc.onicecandidate = e => {
        if (e.candidate)
            hub.invoke('IceCandidate', e.candidate.candidate,
                       e.candidate.sdpMid, e.candidate.sdpMLineIndex);
    };

    const offer = await pc.createOffer();
    await pc.setLocalDescription(offer);

    const serverIn  = document.getElementById('serverInput').value;
    const serverOut = document.getElementById('serverOutput').value;
    const answer = await hub.invoke('Offer', offer.sdp, serverIn, serverOut);

    await pc.setRemoteDescription({ type: 'answer', sdp: answer });

    document.getElementById('btnConnect').disabled = true;
    document.getElementById('btnDisconnect').disabled = false;
    setStatus('Verbonden ✓', 'ok');
};

document.getElementById('btnDisconnect').onclick = async () => {
    if (pc) { pc.close(); pc = null; }
    await hub.stop();
    document.getElementById('btnConnect').disabled = false;
    document.getElementById('btnDisconnect').disabled = true;
    setStatus('Verbroken.');
};

function setStatus(msg, cls = '') {
    const el = document.getElementById('status');
    el.textContent = msg;
    el.className = cls;
}

init();
