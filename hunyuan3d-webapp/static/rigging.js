(() => {
  const panel = document.querySelector('#rigging-panel');
  const form = document.querySelector('#rigging-form');
  if (!panel || !form) return;
  const get = id => document.getElementById(id);
  const start = get('rigging-start'), label = get('rigging-status-label'), message = get('rigging-message');
  const result = get('rigging-result'), viewer = get('rigging-viewer'), motion = get('rigging-motion');
  const action = get('rigging-action'), speed = get('rigging-speed'), reset = get('rigging-reset');
  const timeline = get('rigging-timeline'), time = get('rigging-time'), state = get('motion-state');
  const download = get('rigging-download'), motionDownload = get('motion-download');
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  let timer, frame;
  let requesting = false, playing = false, ready = false, clips = [];
  const selected = () => clips.find(clip => clip.name === action.value);
  const seek = seconds => {
    // model-viewer forwards to Three's AnimationMixer.setTime, which scales
    // the requested time by timeScale. Seek in unscaled animation seconds.
    viewer.currentTime = seconds / Number(speed.value);
  };
  const updateTime = () => {
    const duration = selected()?.duration || viewer.duration || 0;
    const current = Math.min(Math.max(Number(viewer.currentTime) || 0,0),duration);
    timeline.value = duration ? Math.round(current/duration*1000) : 0;
    time.textContent = `${current.toFixed(1)} / ${duration.toFixed(1)}초`;
    timeline.setAttribute('aria-valuetext', `${current.toFixed(1)}초 / ${duration.toFixed(1)}초`);
  };
  const tick = () => { updateTime(); if (playing) frame = window.requestAnimationFrame(tick); };
  const setPlaying = value => {
    playing = Boolean(value && ready && clips.length);
    window.cancelAnimationFrame(frame);
    if (playing) { viewer.play(); frame = window.requestAnimationFrame(tick); }
    else viewer.pause();
    motion.setAttribute('aria-pressed',String(playing));
    motion.textContent = playing ? '일시정지' : '재생';
    state.textContent = ready ? `${playing ? '재생 중' : '일시정지'} · ${selected()?.label || '동작'}` : '모델을 불러오는 중';
    updateTime();
  };
  const enable = () => [motion,action,speed,reset,timeline].forEach(control => { control.disabled = !ready || !clips.length; });
  const choose = autoplay => {
    if (!ready || !clips.length) return;
    viewer.pause(); viewer.animationName = action.value;
    viewer.currentTime = 0; viewer.timeScale = Number(speed.value);
    setPlaying(autoplay && !reducedMotion.matches);
  };
  viewer.addEventListener('load', () => { ready = true; enable(); choose(true); });
  const refresh = async () => {
    window.clearTimeout(timer);
    try {
      const response = await fetch(panel.dataset.statusUrl);
      if (!response.ok) throw new Error('리깅 상태를 읽지 못했습니다.');
      const rig = await response.json(); render(rig);
      if (['queued','running'].includes(rig.status)) timer = window.setTimeout(refresh,3000);
    } catch (error) {
      message.textContent = `${error.message} 잠시 후 다시 확인합니다.`;
      timer = window.setTimeout(refresh,8000);
    }
  };
  const render = rig => {
    const active = ['queued','running'].includes(rig.status);
    start.disabled = active || !rig.ready || requesting;
    start.textContent = active ? '리깅 처리 중' : rig.status === 'completed' ? '다시 리깅하기' : 'AI 자동 리깅 시작';
    form.querySelectorAll('select,input').forEach(input => { input.disabled = active; });
    label.textContent = !rig.ready ? '설치·검증 준비 중' : {queued:'GPU 대기 중',running:'리깅 진행 중',completed:'리깅 완료',failed:'리깅 실패'}[rig.status] || '실행 준비 완료';
    panel.dataset.rigStatus = rig.status;
    message.textContent = rig.message || '설정을 고르고 자동 리깅을 시작하세요.';
    if (!rig.download_url) return;
    result.classList.remove('hidden'); download.href = rig.download_url;
    get('rigging-editor-link').classList.toggle('hidden',!rig.editor_url);
    if(rig.editor_url) get('rigging-editor-link').href=rig.editor_url;
    get('rigging-metrics').textContent = `뼈대 ${rig.bone_count}개 · ${Number(rig.skinned_vertices).toLocaleString()}개 정점에 가중치 적용 · ${Number(rig.faces).toLocaleString()}면 · ${rig.preserve_original ? '원본 유지' : '가벼운 모델'} · ${rig.elapsed_seconds || 0}초`;
    const nextClips = rig.motion_clips || [{name:'JointCheck',label:'관절 테스트',duration:2.4}];
    const changed = JSON.stringify(clips) !== JSON.stringify(nextClips);
    if (changed) {
      clips = nextClips;
      action.replaceChildren(...clips.map(clip => {
        const option = document.createElement('option'); option.value = clip.name; option.textContent = clip.label; return option;
      }));
      action.value = clips.some(clip => clip.name === 'Wave') ? 'Wave' : clips[0].name;
    }
    motionDownload.classList.toggle('hidden',!rig.motion_download_url);
    if (rig.motion_download_url) motionDownload.href = rig.motion_download_url;
    get('motion-note').textContent = rig.motion_download_url
      ? `원본 형상·UV 유지 · 미리보기 텍스처 2K · 다운로드는 원본 품질과 ${clips.length}개 동작 포함`
      : '관절 테스트용 미리보기입니다. 동작 프리셋은 새 리깅 결과에 포함됩니다.';
    if (viewer.getAttribute('src') !== rig.preview_url) {
      setPlaying(false); ready = false; enable(); viewer.animationName = action.value; viewer.src = rig.preview_url;
    } else if (changed && viewer.loaded) { ready = true; enable(); choose(true); }
  };
  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (requesting || start.disabled) return;
    requesting = true; start.disabled = true; label.textContent = '대기열 등록 중';
    try {
      const response = await fetch(form.action,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({
        preserve_original:get('rig-preserve').value === 'true', analysis_faces:Number(get('rig-faces').value), seed:Number(get('rig-seed').value),
      })});
      const body = await response.json();
      if (!response.ok) throw new Error(body.error || '리깅을 시작하지 못했습니다.');
      requesting = false; render(body); timer = window.setTimeout(refresh,1000);
    } catch (error) {
      requesting = false; start.disabled = false; label.textContent = '시작하지 못했습니다.'; message.textContent = error.message;
    }
  });
  motion.addEventListener('click', () => setPlaying(!playing));
  action.addEventListener('change', () => choose(true));
  speed.addEventListener('change', () => { viewer.timeScale = Number(speed.value); });
  reset.addEventListener('click', () => { setPlaying(false); viewer.currentTime = 0; updateTime(); });
  timeline.addEventListener('input', () => {
    const position = Number(timeline.value);
    setPlaying(false); seek(position/1000*(selected()?.duration || viewer.duration || 0)); updateTime();
  });
  reducedMotion.addEventListener('change', () => { if (reducedMotion.matches) setPlaying(false); });
  viewer.addEventListener('error', () => {
    setPlaying(false); ready = false; enable(); state.textContent = '미리보기 로드 실패';
    message.textContent = '미리보기를 불러오지 못했습니다. 동작 포함 GLB를 내려받아 확인할 수 있습니다.';
  });
  document.addEventListener('generation-job-completed',refresh);
  document.querySelector('.rig-advanced').addEventListener('invalid', () => { document.querySelector('.rig-advanced').open = true; },true);
  if (!panel.classList.contains('hidden')) refresh();
})();
