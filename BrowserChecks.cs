using Microsoft.Web.WebView2.Core;
using System.Text.Json;

namespace GPTCursor;

internal sealed partial class MainForm
{
    internal async Task CheckBrowserInterface(string output)
    {
        var report = new List<string>();
        var timeout = DateTime.UtcNow.AddSeconds(30);
        while (!browserReady && DateTime.UtcNow < timeout) await Task.Delay(100);
        if (!browserReady) throw new InvalidOperationException("Browser initialization failed: " + status.Text);
        async Task Check(string expression, string description)
        {
            string result = await browser!.ExecuteScriptAsync(expression);
            if (result != "true") throw new InvalidOperationException(description + ": " + result);
            report.Add("PASS: " + description);
        }
        async Task Act(string script) { await browser!.ExecuteScriptAsync(script); await Task.Delay(150); }
        async Task Capture(string name)
        {
            await Task.Delay(200);
            using var file = File.Create(Path.Combine(output, name + ".png"));
            await browser!.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
        }
        await Task.Delay(400);
        if (browser!.Width < ClientSize.Width - 24) throw new InvalidOperationException("Browser does not cover the full client area");
        report.Add("PASS: Browser covers the full client area without legacy layout gutters");
        ClientSize = new Size(1200, 800);
        await Check("document.querySelectorAll('.nav-button').length === 4", "Four settings pages loaded through the real host bridge");
        await Check("document.fonts.check('14px \"OpenAI Sans\"')", "OpenAI Sans font loaded locally");
        await Check("document.querySelector('canvas').width > 0 && document.querySelector('canvas').height > 0", "Live preview canvas initialized");
        await Capture("light-en-cursor");
        await Act("document.querySelector('input[type=range]').value=48;document.querySelector('input[type=range]').dispatchEvent(new Event('input'))");
        if (preferences.Size != 48 || engine.Size != 48) throw new InvalidOperationException("Slider did not reach cursor engine");
        report.Add("PASS: Size changes reach both stored preferences and cursor engine");
        await Act("document.querySelector('[aria-label=\"Direction style\"]').click()");
        await Check("document.querySelectorAll('.option').length === 3", "Custom direction popover shows all choices");
        await Act("document.querySelectorAll('.option')[1].click()");
        if (preferences.Direction != DirectionStyle.ReturnToRest) throw new InvalidOperationException("Direction bridge failed");
        report.Add("PASS: Direction selection reaches native settings");
        await Act("document.querySelectorAll('.nav-button')[2].click()");
        await Act("document.querySelector('[aria-label=\"Show click rings (both buttons)\"]').click()");
        if (preferences.ClickRings) throw new InvalidOperationException("Ring toggle bridge failed");
        report.Add("PASS: Click-ring switch updates native settings independently");
        await Act("document.querySelectorAll('.nav-button')[3].click()");
        await Act("document.querySelector('[aria-label=Appearance]').click()");
        await Act("document.querySelectorAll('.option')[2].click()");
        await Check("document.documentElement.dataset.theme === 'dark'", "Dark theme applies through the host");
        if (preferences.Theme != "dark") throw new InvalidOperationException("Theme not persisted in model");
        await Act("document.querySelector('[aria-label=Language]').click()");
        await Act("document.querySelectorAll('.option')[1].click()");
        await Check("document.documentElement.lang === 'de' && document.querySelector('#page-title').textContent === 'System'", "German translation keeps the active page");
        await Act("document.querySelector('.keycaps').click()");
        await Check("document.querySelector('.keycaps').textContent.includes('…')", "Shortcut recording starts through the web button");
        await Act("document.dispatchEvent(new KeyboardEvent('keydown',{key:'F9',keyCode:120,ctrlKey:true,altKey:true,bubbles:true}))");
        if (preferences.ShortcutKey != 120 || preferences.ShortcutModifiers != 3 || recording) throw new InvalidOperationException("Shortcut recording failed");
        report.Add("PASS: Shortcut editor validates and retains the recorded keys");
        await Capture("dark-de-system");
        await Act("document.querySelector('#page-scroll').scrollTop=9999");
        await Check("[...document.querySelectorAll('button')].some(b=>b.textContent==='Nach Updates suchen' && b.getBoundingClientRect().bottom<innerHeight)", "Update actions remain reachable by scrolling");
        await Capture("dark-de-updates");
        ClientSize = new Size(800, 620);
        await Act("document.querySelectorAll('.nav-button')[1].click()");
        await Check("document.documentElement.scrollWidth <= innerWidth", "Compact layout has no horizontal window overflow");
        await Capture("dark-de-motion-compact");
        await Check("getComputedStyle(document.querySelector('.surface')).backgroundColor !== getComputedStyle(document.querySelector('.sidebar')).backgroundColor", "Sidebar and main surface retain distinct layers");
        await Check("getComputedStyle(document.body).backgroundImage.includes('gradient')", "Outer background gradient present");
        File.WriteAllLines(Path.Combine(output, "checks.txt"), report);
    }
}
