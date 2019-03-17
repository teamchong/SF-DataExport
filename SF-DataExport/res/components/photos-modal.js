Vue.component('photos-modal', {
    template,
    computed: {
        currentInstanceUrl() {
            return this.$store.state.currentInstanceUrl;
        },
        userCount() { return this.$store.state.users.length; },
        userPhotos() {
            const { currentInstanceUrl, showPhotosModal, users } = this.$store.state;
            if (!showPhotosModal) {
                return [];
            }
            return users
                .filter(u => u.FullPhotoUrl && !/\.content\.force\.com\/profilephoto\/005\//i.test(u.FullPhotoUrl))
                .map(u => ({
                    id: u.Id,
                    name: u.Name,
                    role: (u.UserRole || {}).Name || '',
                    profile: (u.Profile || {}).Name || '',
                    photo: u.FullPhotoUrl
                }));
        },
    },
});