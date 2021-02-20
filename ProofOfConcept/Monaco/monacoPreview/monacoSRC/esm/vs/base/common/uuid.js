// prep-work
const _data = new Uint8Array(16);
const _hex = [];
for (let i = 0; i < 256; i++) {
    _hex.push(i.toString(16).padStart(2, '0'));
}
// todo@jrieken
// 1. node nodejs use`crypto#randomBytes`, see: https://nodejs.org/docs/latest/api/crypto.html#crypto_crypto_randombytes_size_callback
// 2. use browser-crypto
const _fillRandomValues = function (bucket) {
    for (let i = 0; i < bucket.length; i++) {
        bucket[i] = Math.floor(Math.random() * 256);
    }
    return bucket;
};
export function generateUuid() {
    // get data
    _fillRandomValues(_data);
    // set version bits
    _data[6] = (_data[6] & 0x0f) | 0x40;
    _data[8] = (_data[8] & 0x3f) | 0x80;
    // print as string
    let i = 0;
    let result = '';
    result += _hex[_data[i++]];
    result += _hex[_data[i++]];
    result += _hex[_data[i++]];
    result += _hex[_data[i++]];
    result += '-';
    result += _hex[_data[i++]];
    result += _hex[_data[i++]];
    result += '-';
    result += _hex[_data[i++]];
    result += _hex[_data[i++]];
    result += '-';
    result += _hex[_data[i++]];
    result += _hex[_data[i++]];
    result += '-';
    result += _hex[_data[i++]];
    result += _hex[_data[i++]];
    result += _hex[_data[i++]];
    result += _hex[_data[i++]];
    result += _hex[_data[i++]];
    result += _hex[_data[i++]];
    return result;
}
