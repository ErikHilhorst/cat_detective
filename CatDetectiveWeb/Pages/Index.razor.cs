using CatDetective;
using CatDetective.Systems;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CatDetectiveWeb.Pages
{
    public partial class Index : ComponentBase
    {
        private Game1? _game;

        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);
            if (!firstRender)
                return;

            // JS interop must be live before the game constructs: LoadContent
            // reads settings from localStorage on the first Tick.
            BrowserStorage.Initialize(JsRuntime);
            BrowserAudio.Initialize(JsRuntime);

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
                _game = new Game1();
                _game.Run();
            }

            _game.Tick();
        }
    }
}
