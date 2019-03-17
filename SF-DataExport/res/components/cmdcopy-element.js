Vue.component('cmdcopy-element', {
    template,
    data() {
        return { id:'cmdcopy-' + _.uniqueId() };
    },
    props: ['label', 'cmd'],
    computed: {
        hasOfflineAccess() {
            const { currentInstanceUrl } = this.$store.state;
            return !!currentInstanceUrl && this.orgHasOfflineAccess(currentInstanceUrl);
        }
    }
});