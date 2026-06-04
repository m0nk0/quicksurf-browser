(function() {
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
    
    var lastSelectedText = '';
    
    function init() {
        if (!document.body) {
            setTimeout(init, 100);
            return;
        }
        
        console.log('QuickSurf: Floating menu ready');
        
        var menu = document.createElement('div');
        menu.id = 'quickSurfFloatingMenu';
        menu.innerHTML = '<div style="background: #FF8C42; color: white; padding: 8px 16px; border-radius: 20px; font-size: 14px; font-family: system-ui, sans-serif; cursor: pointer; box-shadow: 0 2px 8px rgba(0,0,0,0.2); display: flex; align-items: center; gap: 8px;"><span>🤖</span><span>Спросить AI</span></div>';
        menu.style.position = 'absolute';
        menu.style.zIndex = '999999';
        menu.style.display = 'none';
        document.body.appendChild(menu);
        
        var menuDiv = menu.firstChild;
        menuDiv.onclick = function(e) {
            e.stopPropagation();
            console.log('QuickSurf: Button clicked, saved text:', lastSelectedText);
            
            if (lastSelectedText && lastSelectedText.length > 0) {
                try {
                    window.chrome.webview.postMessage('AI_SELECTION:' + lastSelectedText);
                    console.log('QuickSurf: Message sent to C#');
                } catch(e) {
                    console.log('QuickSurf: Error sending message:', e);
                }
            }
            menu.style.display = 'none';
        };
        
        // Отслеживаем выделение текста (без ПКМ)
        document.addEventListener('mouseup', function(e) {
            // Не реагируем на правую кнопку мыши
            if (e.button === 2) return;
            
            setTimeout(function() {
                var selectedText = window.getSelection().toString().trim();
                console.log('QuickSurf: Text selected:', selectedText);
                
                if (selectedText && selectedText.length > 0) {
                    lastSelectedText = selectedText;
                    
                    var menuEl = document.getElementById('quickSurfFloatingMenu');
                    if (!menuEl) return;
                    
                    clearTimeout(window.hideTimeout);
                    var range = window.getSelection().getRangeAt(0);
                    var rect = range.getBoundingClientRect();
                    if (rect.width > 0 && rect.height > 0) {
                        var scrollTop = window.scrollY || document.documentElement.scrollTop;
                        var scrollLeft = window.scrollX || document.documentElement.scrollLeft;
                        menuEl.style.display = 'block';
                        menuEl.style.left = (rect.left + scrollLeft + (rect.width / 2) - 70) + 'px';
                        menuEl.style.top = (rect.top + scrollTop - 45) + 'px';
                        console.log('QuickSurf: Menu shown');
                    }
                } else {
                    window.hideTimeout = setTimeout(function() {
                        var menuEl2 = document.getElementById('quickSurfFloatingMenu');
                        if (menuEl2) {
                            menuEl2.style.display = 'none';
                        }
                    }, 300);
                }
            }, 10);
        });
        
        // Отслеживаем нажатие правой кнопки - скрываем меню
        document.addEventListener('contextmenu', function() {
            var menuEl = document.getElementById('quickSurfFloatingMenu');
            if (menuEl) {
                menuEl.style.display = 'none';
            }
        });
        
        document.addEventListener('scroll', function() {
            var menuEl = document.getElementById('quickSurfFloatingMenu');
            if (menuEl && menuEl.style.display === 'block') {
                menuEl.style.display = 'none';
            }
        });
        
        document.addEventListener('click', function(e) {
            var menuEl = document.getElementById('quickSurfFloatingMenu');
            if (menuEl && menuEl.style.display === 'block' && !menuEl.contains(e.target)) {
                menuEl.style.display = 'none';
            }
        });
        
        console.log('QuickSurf: Floating menu created');
    }
})();