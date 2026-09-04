mergeInto(LibraryManager.library, {
    IsMobileDevice: function () {
        var userAgent = (navigator.userAgent || navigator.vendor || window.opera || "").toLowerCase();
        
        // Deteccion comun de dispositivos moviles
        var isMobile = /android|iphone|ipad|ipod|windows phone|iemobile|mobile/i.test(userAgent);
        
        // Soporte adicional para iPadOS moderno (que envia userAgent similar a Macintosh pero con multitouch)
        if (!isMobile && navigator.maxTouchPoints && navigator.maxTouchPoints > 2 && /macintosh|mac os x/i.test(userAgent)) {
            isMobile = true;
        }

        return isMobile;
    }
});
