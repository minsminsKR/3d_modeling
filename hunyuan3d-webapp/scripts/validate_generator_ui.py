"""Isolated request serialization checks. Never invokes a generation worker."""
from pathlib import Path
import io
import json
import sys
import importlib.util
from werkzeug.datastructures import MultiDict

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
spec = importlib.util.spec_from_file_location('ui_validation_app', root / 'app.py')
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
source = (root / 'app.py').read_text()
assert source.count('app = create_app()') == 1
exec(compile(source.replace('app = create_app()', 'app = None'), str(root / 'app.py'), 'exec'), module.__dict__)
data = root / 'instance' / 'DATA-20261011-A100-002-UI'
assert not (data / 'jobs.json').exists(), 'Retain existing validation output; use a new DATA run directory'
module.INSTANCE_DIR = data
module.UPLOAD_DIR = data / 'uploads'
module.OUTPUT_DIR = data / 'outputs'
module.JOBS_FILE = data / 'jobs.json'
submitted = []

class StubExecutor:
    def __init__(self, **kwargs): pass
    def submit(self, callback, job_id, image_paths, options):
        submitted.append(options.__dict__)

module.ThreadPoolExecutor = StubExecutor
app = module.create_app()
app.template_folder = str(root / 'templates')
app.static_folder = str(root / 'static')
client = app.test_client()
html = client.get('/')
assert html.status_code == 200
assert b'generator.css' in html.data and b'generator.js' in html.data
assert b'data-preset="quality"' in html.data
cases = [
    ('native-default', [('model','trellis2'),('gpu_id','auto'),('trellis_pipeline','1536_cascade'),('trellis_steps','12'),('trellis_texture_size','8192'),('seed','1234'),('remove_background','off'),('remove_background','on')], {'trellis_pipeline':'1536_cascade','trellis_steps':12,'trellis_texture_size':8192,'texture':True,'remove_background':True,'gpu_id':1}),
    ('native-fast', [('model','trellis2'),('gpu_id','auto'),('trellis_pipeline','512'),('trellis_steps','12'),('trellis_texture_size','4096'),('seed','1234'),('remove_background','off'),('remove_background','on')], {'trellis_pipeline':'512','trellis_texture_size':4096,'texture':True,'gpu_mode':'auto'}),
    ('native-balanced-bg-off', [('model','trellis2'),('gpu_id','auto'),('trellis_pipeline','1024_cascade'),('trellis_steps','12'),('trellis_texture_size','4096'),('seed','42'),('remove_background','off')], {'trellis_pipeline':'1024_cascade','trellis_texture_size':4096,'seed':42,'remove_background':False,'gpu_id':1}),
    ('hunyuan-balanced', [('model','hunyuan3d-2.1'),('gpu_id','split'),('octree_resolution','512'),('num_inference_steps','50'),('guidance_scale','7.5'),('num_chunks','50000'),('texture_profile','detail'),('texture','on'),('seed','1234'),('remove_background','off'),('remove_background','on')], {'num_inference_steps':50,'octree_resolution':512,'num_chunks':50000,'texture_profile':'detail','gpu_id':0,'texture_gpu_id':1,'texture':True}),
    ('hunyuan-quality', [('model','hunyuan3d-2.1'),('gpu_id','split'),('octree_resolution','512'),('num_inference_steps','50'),('guidance_scale','7.5'),('num_chunks','50000'),('texture_profile','ultra'),('texture','on'),('seed','1234'),('remove_background','off'),('remove_background','on')], {'texture_profile':'ultra','texture_gpu_id':1,'texture':True}),
    ('hunyuan-fast-shape-only', [('model','hunyuan3d-2.1'),('gpu_id','split'),('octree_resolution','256'),('num_inference_steps','25'),('guidance_scale','7.5'),('num_chunks','50000'),('texture_profile','standard'),('seed','1234'),('remove_background','off')], {'num_inference_steps':25,'octree_resolution':256,'texture':False,'remove_background':False}),
    ('omni-balanced', [('model','hunyuan3d-omni'),('gpu_id','split'),('octree_resolution','512'),('num_inference_steps','50'),('guidance_scale','7.5'),('num_chunks','50000'),('texture_profile','detail'),('texture','on'),('seed','1234'),('remove_background','off'),('remove_background','on')], {'model':'hunyuan3d-omni','omni_use_ema':True,'texture':True}),
]
results = []
for name, fields, expected in cases:
    request = MultiDict(fields)
    request.add('images', (io.BytesIO(b'ui serialization test - executor stubbed'), name+'.png'))
    response = client.post('/jobs', data=request, content_type='multipart/form-data')
    assert response.status_code == 302, (name,response.status_code,response.data)
    actual = submitted[-1]
    assert all(actual[k] == v for k,v in expected.items()), (name,actual,expected)
    results.append({'name':name,'status_code':response.status_code,'options':actual})
# Server guards remain authoritative; these requests cannot run inference.
for name, fields in [
    ('native-high-gpu0-blocked', [('model','trellis2'),('gpu_id','0'),('trellis_pipeline','1536_cascade')]),
    ('hunyuan-high-texture-gpu0-blocked', [('model','hunyuan3d-2.1'),('gpu_id','0'),('texture_profile','ultra'),('texture','on')]),
]:
    request=MultiDict(fields)
    request.add('images',(io.BytesIO(b'guard-test'), name+'.png'))
    response=client.post('/jobs',data=request,content_type='multipart/form-data')
    assert response.status_code==400
    results.append({'name':name,'status_code':response.status_code})
report={'change_id':'CHG-20261011-A100-002','data_id':'DATA-20261011-A100-002-UI','executor_stubbed':True,'model_inference_executed':False,'passed':True,'cases':results,'production_defaults':module.generation_defaults()}
(root/'logs'/'generator-ui-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
print(json.dumps({'passed':True,'cases':len(results),'executor_submissions':len(submitted),'model_inference_executed':False,'isolated_instance':str(data)}))
