  // Your use of the content in the files referenced here is subject to the terms of the license at https://aka.ms/fabric-assets-license

// tslint:disable:max-line-length

import {
  IIconOptions,
  IIconSubset,
  registerIcons
} from '@uifabric/styling';

export function initializeIcons(
  baseUrl: string = '',
  options?: IIconOptions
): void {
  const subset: IIconSubset = {
    style: {
      MozOsxFontSmoothing: 'grayscale',
      WebkitFontSmoothing: 'antialiased',
      fontStyle: 'normal',
      fontWeight: 'normal',
      speak: 'none'
    },
    fontFace: {
      fontFamily: `"FabricMDL2Icons"`,
      src: `url('data:application/octet-stream;base64,d09GRgABAAAAAA08AA4AAAAAF8QAA71xAAAAAAAAAAAAAAAAAAAAAAAAAABPUy8yAAABRAAAAEgAAABgMUZ8OGNtYXAAAAGMAAAAZAAAAYqa74vyY3Z0IAAAAfAAAAAgAAAAKgnZCa9mcGdtAAACEAAAAPAAAAFZ/J7mjmdhc3AAAAMAAAAADAAAAAwACAAbZ2x5ZgAAAwwAAATWAAAHmHpZ9DNoZWFkAAAH5AAAADIAAAA2AbgYW2hoZWEAAAgYAAAAFQAAACQQAQgDaG10eAAACDAAAAAqAAAAKhJIBzVsb2NhAAAIXAAAACgAAAAoD8YSMG1heHAAAAiEAAAAHQAAACAANwH2bmFtZQAACKQAAAP3AAAJ+pOT8VVwb3N0AAAMnAAAABQAAAAg/1EAinByZXAAAAywAAAAiQAAANN4vfIOeJxjYGG/yTiBgZWBgXUWqzEDA6M0hGa+yJDGJMTBysrFyMQIBgxAIMCAAL7BCgoMDs8ZvqpygPkQkgGsjgXCU2BgAADpBAgmeJxjYGBgZoBgGQZGBhBoAfIYwXwWhgwgLcYgABRhe87wnPc533PB58LPc16WvLL6sPCr6v//DAxI4tkvc2HikowSXyW+S3yS+CgxW2y26DX+Aq63UPOxAEY2XDIjBwAAThsj73icY9BiCGUoYGhgWMXIwNjA7MB4gMEBiwgQAACqHAeVeJxdj79Ow0AMxnMktIQnQDohnXUqQ5WInemGSyTUJSUM56WA1Eqk74CUhcUDz+JuGfNiCMwR/i62v8/6fL9zp/nJfHacpUcqKVacN+Gg1AsO6u2Z/fkhT+82ZWFM1XlW92XBagmia04X9U2waMjQ9ZZMbR4ftpwtYpfFjvDScNKGTuptAHaov8cd4lU8ksUjhBLfT/F9jEv6tSxWhtOLJqwD916z86gBTMVjE3j0GhB/yKQ/dWcT42w5ZdvATnOCRJ/KAvdEmoT7S49/9aCS/4b7bci/q0H1Tdz0FvSHYcGCsKGXZ9tQCRpg+Q6E/GTGAAEAAgAIAAr//wAPeJyVVU1sE1cQnnlvd1/cEqebtbNxQp2sHdtYgKFxnIVShSCBgKpEVVtx2CgoF3Ko1FwqkCIh5SFVUCrlErj1EIFVbu0BJNSUQ3Pjyo0DarGEIBc4JFQqNm83nV3bqaP00vXOvp/53vx5Zh4wuAugfadfBg4CwDUdM+eYzl3+p3rAHvifgn658cMtbQro4QAou8CgAxCDOBThYbTLQdv/K8AHq9ANxl+r8B5w+nZFc53mhz9Cp+Ikze1PF7yFf0mXUiqQkuHiIiBY+FK8Ng7SSUA7hi6Km56ylOXxm8ZBmvHXnvqGcG/gjegW3fA+QAxFDAuEjaGtTbN7nn/OP+exe/6Ux+6z+54GO5b+FEEg9MIAQzREA1z4Fq7Dj6Qx0ZdMGEmd5wxh6TxjZDP5LM9Z+QJtjeUrY+OVnKWPu7Q1Ol4e7SsTqM/OWTiBlbF8wS3oJSxYBVHCbMYQBWHFUeSEHcdkos8Wdi6NNrfdNJZHx13b5RPo6i6OplkyEWfZTIlVxiaYNTpBY4nWcdpPM+NvROQrwZP+gZPB1dux3hi9t4OrJwf6gycryBEDfwWLxMXFNhcXiYvFleB7pmns1flgY+hE/vjjW/Zhu/9Q/83Hx/OTw8HG+VdNLvbs5mLPebU8dWPu2LG5G1Pt0Z09UyyemXVbozby/4zxOx0J/iBdpHFb3audpnZyd5nKvu40Kxx92WFYNFKOcMaFb0yHuRRjlCCiqmbUjMd97jPO76gLHg8UC3Mb8Sfgui/8EJuLhZmnVxXjgacu8Ds8POGFZyMso5w3prexlHYd0mZ4lVc9xRWPsCBwRtSN02HZNPMUNeD1TVVX9U1ODCU2uFD1DV5v41cJXw3xUf7TIbGphBKbXHBhVDeU4PUNVeeiafcq0KaoR/IZybeNashswoRo6SGNhJ0h7Ok2NiwYNsXrbQNEfVsLYSHFUmLdWNjGCjGvltXyAl/n6wssxeejuUothFi8Ltb19aaPYRWSj8Th83x+QRGTTqjlaN60+Tqk9HUR4ZuxFvq8SrVRqUhFpIxinYKUsdDCNmuckeK29m0lJDr6I7voB4NU0YCjWpr1JhMszrRsZqTEqKp6J9gI1ZdG9UV7cY3qS9NgCY3nD6+dPXvt4fOgsbQUNNorNJauvHhUvTQ5ean66MWVjnkX/Ce6Jemt3HWgNQ9NjDXtFBL2wxH4BCbhJJyBz+Bz+IqsNrOmQ5ZlHbOcjFqCST2h7FCTsNM8GnVqEG6JEwKpVeeoZWPSqbSJh2uiLpD+5JFThzLFjw/5w1IOlrKJRLY0yGq5ycoBszs1VPiQrYW772YHc06mr8fJ7+/3hxEMWZcGNKQOgUTYgpCkLxktjWhp7c3stZSUoTgdQhGDB4+m7ZFUd7jTOzC0xzzq7tsi/DspuVSSU3vfahEDEiUDukoCCdBDoZBRLML7Zw9YMAT74AAchrEdsQGLXHWdGKsUhI0m5YFtYiZsyNSFqWWjSYVFdwuFJOlkaaK3iGfNMsU0hWUzmyQqUozoymmQYXP+b0FNk7WLz969xGFcW6sFawP5/AARvxv87OEXwVMGKoqBDP0OOr50aQkZPc0g6UC+slNBLXg69+xijctgGoeCE7/X2NwWRDKJGGAu+MXDL8l/Bq3jOwSHL4XkH5FAEYUAAHicY2BkYGBg3lv4qtx+VTy/zVcGbg4GENj/92ADiL410ycHRHMwgMU5GZhAFABm+QpyAAB4nGNgZGDgYAABOMnIgAqYAALKAB0AAAAFKgCmCAAAAAATAPMABgADAaUCAwAHAAcBuQFdABkAGQGHAhkAAAAAAAAAAAAAABYASABcAH4BbgGEAZoBsAHIAeAB9gIMAiICOgJQAmYCugNAA8x4nGNgZGBgEGaYw8DPAAKMYJILhBkjQUwAFV0BLwAAAHictVQ9ixxHEK29XenOyDqMwaCwA2NOxzIrnQSHpeiQrEiXnMSBEkPvTO9so9npobtHyxgHDhX4ZzgR+FcYGxw69i9w7MihX9X03IduLc4G77A9r6vr81X1ENGd0Rc0ov73AP8ej+hT7Hq8Rdv0VcJjyJ8nPAH+OuEb9DE1Cd+kT+jbhLfpS/o+4R36jH5J+Bbt0+8J3x79PJokvEv7W78iymjyEXbF1p8Jj+jz8WnCW7Q7/ibhMeRvE54A/5jwDboz/i3hm6TGfyS8TX6yk/AO7U8GP7fo5eSHhG+P307+SniXXu5899M7dXDv/qE6trl3wS2ieuJ847yO1tWZOqoqdWLLZQzqxATj35gie6bn3ubq+OnzA3UUgonhxJRtpf3Vg6uSU+MDPKsH2eHD/pQP+7MXpnRG2aC0il4XZqX9a+UWKi7NhfxK79qGxblbNbq2JmQbk1/G2DyazdbrdbYazjPYzGLXuNLrZtnNFq6OYXZuHtqmqawpFB9k6pVr1Up3qg0GSSAxFqvoVO6NjmaqChuaSndTpetCNd7iNIeKwVsH1Ri/sjHC3byTIiqbm5p94SAo5wew4AjTq6U23hVtHqeKmYftlG2GALZW66XNlxcyWyOorfOqLdCms+xdXXVqz95VZjVHLufq8PChbEW9sHWpvAkRnWJWzwOw+Zmvx8LAnkWUaFbcAm8RtXDrunK6uMye7qkynstxCIW1jU0bVWG4TNZZmqq5zCiGse6SOjcEDsHP0s4tcs6u3216R4oO6B7dp0OgY7KUkydHAf8FRcieAHnceV41JBaopgwnR1ThUXQCWUlLnAXZGbwNtN9gLaD5DHZz7Nk3x3iKL8uB2AfRZDu2KqmFPw3N61hcR+dU8ggpZ4UvXYY6H16yHSwv2r2QbBxWBR2uSuMfhYEC0pVk+RoyZolPlqK7ib9S9i0YHLRzvFfYa+Rkha3sXzDPPEdIH9EMz1qeDP7et89SnBlwJ15K8dPAQwfpQrxxtbON0YPk3KAjVvqoziy496+kJiVMdHi3wl3PRM/YoM0yJ1V7aHAdhqbYF6LXSMc7kTAfHKeRzvS2efJi0l6L70b6yjVHOWOrueQxdKKSithqyKu3CNIFf0WyOKtheq2uNrIvYJNjPxW++pnv407P4rxfgZVJXAtPOdbNnK1Tpaydo5pW5q7YyD3bVIL2oH8Xb57QeeJlk/c+h//K7bn3QjyVkHmZ45ju1DCrmyoYol/N6/GFGeBK+lqixBtuAfvvay0gWUvlTm7lh2ZPX5oqI31xae2r6nErN6sVS8526ObghzUrucn/PKP9l7FOnTn3PtwQm1jm+eF858J039v/4W7/DWw1OJYAeJxjYGYAg/9+DOUMmEAYACk9Adt4nNvAoM2wiZGTSZtxExeI3M7Vmhtqq8rAob2dOzXYQU8GxOKJ8LDQkASxeJ3NteWFQSw+HRUZER4Qi19OQpiPA8QS4OPhZGcBsQTBAMQS2jChIMAAyGLYzgg3mgluNDPcaBa40axwo9nkJKFGs8ON5oAbzQk3epMwI7v2BgYF19pMCRcAxAEoGgAAAA==') format('truetype')`
    },
    icons: {
      'GlobalNavButton': '\uE700',
      'ChevronDown': '\uE70D',
      'ChevronUp': '\uE70E',
      'Cancel': '\uE711',
      'Settings': '\uE713',
      'ChevronLeft': '\uE76B',
      'ChevronRight': '\uE76C',
      'ChevronUpSmall': '\uE96D',
      'ChevronDownSmall': '\uE96E',
      'ChevronLeftSmall': '\uE96F',
      'ChevronRightSmall': '\uE970',
      'ChevronUpMed': '\uE971',
      'ChevronDownMed': '\uE972',
      'ChevronLeftMed': '\uE973',
      'ChevronRightMed': '\uE974',
      'CircleRing': '\uEA3A',
      'FabricReportLibrary': '\uF0A1',
      'PictureStretch': '\uF525'
    }
  };

  registerIcons(subset, options);
}
