from pathlib import Path
t = Path('static/vendor/model-viewer-4.4.0.min.js').read_text()
for key in ['set currentTime','setAnimationTime(','set timeScale']:
    i = t.find(key)
    print(key,t[max(0,i-150):i+800])
