Vue.component('file-element', {
    template,
    props: ['value', 'label'],
    data() { return { id: 'file-' + _.uniqueId(), path: null, items: [], loading: false }; },
    watch: {
        async path(search) {
            const { itemsField: field } = this;
            this.$emit('input', search);
            if (search) {
                this.loading = true;
                this.items = await subscribeDispatch([{
                    type: 'FetchFilePath', payload: {
                        search, field
                    }
                }]);
                this.loading = false;
            }
        }
    },
    computed: {
        model: {
            get() { return this.value; },
            set(value) { this.$emit('input', value); }
        }
    }
});