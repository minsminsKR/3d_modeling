(() => {
  const form = document.querySelector('#generation-form');
  if (!form) return;
  const model = form.elements.model;
  const field = name => form.elements.namedItem(name);
  const texture = field('texture');
  const textureUnavailable = texture.disabled;
  const gpu = field('gpu_id');
  const pipeline = field('trellis_pipeline');
  const defaults = Object.fromEntries([...form.querySelectorAll('select, input[type="number"]')].map(input => [input.name, input.value]));
  const backgroundDefault = document.querySelector('#remove-background').checked;
  let imageCount = 0;
  let hunyuanTexture = texture.checked;
  let previousModel = model.value;
  const nativePresets = {
    fast: {trellis_pipeline: '512', trellis_texture_size: '4096', trellis_steps: '12'},
    balanced: {trellis_pipeline: '1024_cascade', trellis_texture_size: '4096', trellis_steps: '12'},
    quality: {trellis_pipeline: '1536_cascade', trellis_texture_size: '8192', trellis_steps: '12'},
  };
  const hunyuanPresets = {
    fast: {octree_resolution: '256', texture_profile: 'standard', num_inference_steps: '25'},
    balanced: {octree_resolution: '512', texture_profile: 'detail', num_inference_steps: '50'},
    quality: {octree_resolution: '512', texture_profile: 'ultra', num_inference_steps: '50'},
  };
  const write = (id, text) => { const element = document.getElementById(id); if (element) element.textContent = text; };
  const presets = () => model.value === 'trellis2' ? nativePresets : hunyuanPresets;
  const defaultGpu = () => {
    const mode = model.value === 'trellis2' && pipeline.value === '512' ? 'auto' : model.selectedOptions[0]?.dataset.defaultGpuMode;
    if ([...gpu.options].some(option => option.value === mode && !option.disabled)) gpu.value = mode;
  };
  const sync = () => {
    const native = model.value === 'trellis2';
    for (const [ids, visible] of [
      [['trellis-controls', 'trellis-advanced-controls'], native],
      [['hunyuan-texture-controls', 'hunyuan-shape-controls', 'hunyuan-chunks-controls'], !native],
    ]) for (const id of ids) {
      const container = document.getElementById(id);
      container.classList.toggle('hidden', !visible);
      container.querySelectorAll('input, select').forEach(input => { input.disabled = !visible; });
    }
    texture.disabled = native || textureUnavailable;
    if (native) texture.checked = true;
    const highNative = native && pipeline.value !== '512';
    const gpu0 = gpu.querySelector('option[value="0"]');
    const highHunyuanTexture = !native && texture.checked && !textureUnavailable && field('texture_profile').value !== 'standard';
    if (gpu0) { gpu0.disabled = highNative || highHunyuanTexture; if (gpu0.disabled && gpu.value === '0') gpu.value = '1'; }
    const split = gpu.querySelector('option[value="split"]');
    const auto = gpu.querySelector('option[value="auto"]');
    if (split) split.textContent = native ? '두 GPU 분담 · 형상 1 / 재질 0' : '두 GPU 분담 · 형상 0 / 텍스처 1';
    if (auto) auto.textContent = native ? (highNative ? '자동 배정 · GPU 1' : '자동 배정 · 두 GPU 병렬') : '자동 배정 · 두 GPU 병렬';
    write('gpu-mode-hint', native ? (gpu.value === 'split' ? '형상은 GPU 1, 재질은 GPU 0에서 처리합니다. 고해상도 형상을 위한 여유를 확보하고 메시 보정과 재질 복원 일부를 병행합니다.' : highNative ? 'GPU 1 한 장에서 생성합니다. 두 GPU 분담을 선택하면 고해상도 형상을 위한 여유를 더 확보합니다.' : '여러 이미지를 두 GPU에 각각 배정해 동시에 생성합니다. 한 모델을 나누려면 두 GPU 분담을 선택하세요.') : '분담 모드에서는 형상을 GPU 0, 고품질 텍스처를 GPU 1에서 생성합니다.');
    write('model-help', native ? '형상과 PBR 재질을 함께 생성합니다. 이 서버의 기본 모델입니다.' : model.value === 'hunyuan3d-omni' ? '형상 제어를 지원하는 Omni 모델입니다. 현재 EMA 가중치를 사용합니다.' : '이미지 기반 형상 생성 후 PBR 재질을 입힙니다.');
    write('texture-help', native ? 'TRELLIS.2 결과에는 PBR 재질이 항상 포함됩니다.' : textureUnavailable ? '텍스처 의존성 준비 후 사용할 수 있습니다.' : '색상과 재질을 모델에 입힙니다.');
    const specs = native ? ['512 · 4K', '1024 · 4K', '1536 · 8K'] : ['Octree 256 · 4K', 'Octree 512 · 4K', 'Octree 512 · 8K'];
    ['fast', 'balanced', 'quality'].forEach((key, index) => write(`preset-${key}-spec`, specs[index]));
    write('quality-recommend', native ? '기본' : '8K');
    const active = Object.entries(presets()).find(([, values]) => Object.entries(values).every(([name, value]) => field(name).value === value))?.[0];
    form.querySelectorAll('[data-preset]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.preset === active)));
    write('preset-state', active ? {fast: '빠르게', balanced: '균형', quality: '최고 품질'}[active] : '사용자 설정');
    write('preset-help', native ? (pipeline.value === '1536_cascade' ? '세부 형상을 우선합니다. 8K는 파일 크기와 저장 시간이 늘어납니다.' : '같은 12 Steps로 생성하며, 해상도를 낮추면 형상 생성 부담이 줄어듭니다.') : 'Hunyuan 기본은 균형 프리셋입니다. 768은 생성 해상도, 4K·8K는 최종 텍스처 크기입니다.');
    const modelName = native ? 'TRELLIS.2' : model.value === 'hunyuan3d-omni' ? 'Hunyuan3D-Omni' : 'Hunyuan3D 2.1';
    const shape = native ? pipeline.value === '1536_cascade' ? '최대 1536' : pipeline.value.split('_')[0] : `Octree ${field('octree_resolution').value}`;
    const map = native ? field('trellis_texture_size').value === '8192' ? '8K PBR' : '4K PBR' : texture.checked && !textureUnavailable ? field('texture_profile').value === 'ultra' ? '8K PBR' : '4K PBR' : '형상만 생성';
    let gpuSummary = `GPU ${gpu.value}`;
    if (gpu.value === 'auto') gpuSummary = highNative ? '자동 · GPU 1' : '자동 · 두 GPU 병렬';
    if (gpu.value === 'split') gpuSummary = native ? 'GPU 1 + 0 분담' : 'GPU 0 + 1 분담';
    const steps = native ? field('trellis_steps').value : field('num_inference_steps').value;
    write('summary-model', modelName); write('summary-shape', shape); write('summary-texture', map); write('summary-gpu', gpuSummary);
    const bg = document.querySelector('#remove-background').checked;
    write('summary-extra', `${steps} Steps${native ? ' / 단계' : ''} · 배경 제거 ${bg ? '켜짐' : '꺼짐'}`);
    write('advanced-preview', `${steps} Steps · Seed ${field('seed').value}`);
    write('submit-summary', `${modelName} · ${shape} · ${map}`);
    write('submit-hint', imageCount ? `이미지 ${imageCount}장 → 모델 ${imageCount}개 · GLB` : '이미지를 선택하면 생성할 수 있습니다.');
  };
  form.querySelectorAll('[data-preset]').forEach(button => button.addEventListener('click', () => {
    Object.entries(presets()[button.dataset.preset]).forEach(([name, value]) => { field(name).value = value; });
    // Choosing a preset restores its model's supported scheduling mode.
    sync(); defaultGpu(); sync();
  }));
  model.addEventListener('change', () => {
    if (previousModel !== 'trellis2') hunyuanTexture = texture.checked;
    if (model.value !== 'trellis2') texture.checked = hunyuanTexture;
    previousModel = model.value;
    sync(); defaultGpu(); sync();
  });
  // A select emits input before change; preserve the previous model's texture
  // choice before sync forces TRELLIS.2's required PBR checkbox on.
  form.addEventListener('input', event => { if (event.target !== model) sync(); });
  form.addEventListener('change', sync);
  document.addEventListener('generation-files-change', event => {imageCount = event.detail; sync();});
  document.querySelector('#reset-settings').addEventListener('click', () => {
    const currentModel = model.value;
    Object.entries(defaults).forEach(([name, value]) => { if (name !== 'model') field(name).value = value; });
    model.value = currentModel;
    document.querySelector('#remove-background').checked = backgroundDefault;
    hunyuanTexture = true; texture.checked = true;
    sync(); defaultGpu(); sync();
  });
  // Keep the selected section open if native form validation finds an invalid advanced value.
  form.addEventListener('invalid', event => { const details = event.target.closest('details'); if (details) details.open = true; }, true);
  sync();
})();
