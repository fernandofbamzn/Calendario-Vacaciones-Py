import os
file_path = 'CalendarioWeb/index.html'
with open(file_path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

new_html = lines[:31] + ['    <script src="app.js"></script>\n'] + lines[1168:]
with open(file_path, 'w', encoding='utf-8') as f:
    f.writelines(new_html)
