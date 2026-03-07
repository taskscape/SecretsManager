(function () {
    var input = document.getElementById('searchInput');
    if (!input) return;
    input.addEventListener('input', function () {
        var q = this.value.toLowerCase();
        document.querySelectorAll('#entryList li').forEach(function (li) {
            var item = li.querySelector('.entry-item');
            li.style.display = (!q || item.dataset.title.includes(q)) ? '' : 'none';
        });
    });
})();
