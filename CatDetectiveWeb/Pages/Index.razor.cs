using CatDetective;
using CatDetective.Systems;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CatDetectiveWeb.Pages
{
    public partial class Index : ComponentBase
    {
        private Game1? _game;
        private bool   _bootPrioritized;

        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);
            if (!firstRender)
                return;

            // JS interop must be live before the game constructs: LoadContent
            // reads settings from localStorage on the first Tick.
            BrowserStorage.Initialize(JsRuntime);
            BrowserAudio.Initialize(JsRuntime);
            WasmScale.Initialize(JsRuntime);

            var jsInProcess = (IJSInProcessRuntime)JsRuntime;
            jsInProcess.InvokeVoid("initRenderJS", DotNetObjectReference.Create(this));
        }

        [JSInvokable]
        public void TickDotNet()
        {
            // Under KNI/Blazor, Run() initializes and returns; Tick() advances
            // exactly one frame and is driven by requestAnimationFrame.
            if (_game == null)
            {
                // Hold the boot until the menu/UI textures are decoded by the
                // page's texture worker (Systems/WebTextures.cs) - LoadContent
                // then builds them instantly instead of blocking on .xnb loads.
                // The HTML loader keeps showing progress meanwhile.
                string state = WebTextures.State;
                if (state == "pending") return;
                if (state == "active")
                {
                    if (!_bootPrioritized)
                    {
                        WebTextures.Prioritize(Game1.BootTextures);
                        _bootPrioritized = true;
                    }
                    var (done, total) = WebTextures.ReadyCount(Game1.BootTextures);
                    if (done < total) return;
                }
                _game = new Game1();
                _game.Run();
            }

            _game.Tick();
        }
    }
}
