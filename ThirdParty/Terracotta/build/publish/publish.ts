/*
  This is free and unencumbered software released into the public domain.

  Anyone is free to copy, modify, publish, use, compile, sell, or
  distribute this software, either in source code form or as a compiled
  binary, for any purpose, commercial or non-commercial, and by any
  means.

  In jurisdictions that recognize copyright laws, the author or authors
  of this software dedicate any and all copyright interest in the
  software to the public domain. We make this dedication for the benefit
  of the public at large and to the detriment of our heirs and
  successors. We intend this dedication to be an overt act of
  relinquishment in perpetuity of all present and future rights to this
  software under copyright law.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
  IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
  OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
  ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
  OTHER DEALINGS IN THE SOFTWARE.

  For more information, please refer to <https://unlicense.org>
*/

export async function main({context, octokit, require}) {
    const {Buffer} = require('node:buffer') as typeof import('node:buffer');

    const _got = require('got') as typeof import('got', {with: {"resolution-mode": "import"}});
    const FormData = require('form-data') as typeof import('form-data');
    type FormData = typeof FormData.prototype;
    const {chunk: _chunk} = require('chunk-data') as typeof import('chunk-data', {with: {"resolution-mode": "import"}});

    const ofChunkedRequest = (form: FormData) => {
        const buffer = form.getBuffer();
        return {
            headers: {
                'content-length': buffer.byteLength.toString(),
                ...form.getHeaders()
            },
            body: _chunk(buffer, 65536),
        }
    };

    // Hacky way to enable async-stack tracking.
    const got = _got.default.extend({
        headers: {
            'user-agent': 'nodejs',
        },
        handlers: [
            (options, next) => {
                Error.captureStackTrace(options.context);
                return next(options);
            }
        ],
        hooks: {
            beforeError: [
                error => {
                    // @ts-ignore
                    error.source = error.options.context.stack.split('\n');
                    return error;
                }
            ]
        }
    });

    const pushUploadingJob = (function () {
        let jobs = new Map<string, { progress: number, estimated: number }>();
        let changed = false;

        setInterval(() => {
            if (!changed) {
                return;
            }

            changed = false;
            console.log(Array.from(jobs.entries()).sort((a, b) => a[0].localeCompare(b[0])).map(([name, {
                progress,
                estimated
            }]) => {
                let count = Math.floor(progress * 10);
                let p = `${name.padEnd(55, ' ')}  ${'\u{1F7E9}'.repeat(count)}${'\u{1F537}'.repeat(10 - count)}  ${(progress * 100).toFixed(2)}%`;
                if (progress !== 1) {
                    p += ` ~${estimated.toFixed(2).padStart(3, ' ')}s left`
                }

                return p;
            }).join('\n') + "\n" + "=".repeat(10));
        }, 15_000).unref();

        return (name: string, cfg: { progress: number, estimated: number }) => {
            changed = true;
            jobs.set(name, cfg);
        }
    }());

    let {
        tag_name: tagName,
        name,
        prerelease,
        body,
        assets: _assets
    } = (await octokit.rest.repos.getLatestRelease(context.repo)).data;

    let assets: { name: string, data: Buffer }[] = await Promise.all(_assets.map(async (asset) => {
        const response = await fetch(asset.url, {
            "headers": {
                "Accept": "application/octet-stream"
            }
        });
        if (!response.ok) {
            throw new Error(`HTTP error: ${response.status}`);
        }

        return {name: asset.name, data: Buffer.from(await response.arrayBuffer())};
    })).then(l => l.filter(e => e));
    console.log(`Gathered ${assets.length} assets`);
    for (let asset of assets) {
        console.log(`- ${asset.name.padEnd(52, ' ')}: ${asset.data.length} bytes`);
    }

    await Promise.all([
        (async () => {
            const {id} = await got(`https://gitee.com/api/v5/repos/${process.env.GITEE_OWNER}/${process.env.GITEE_REPO}/releases`, {
                method: "POST",
                json: {
                    access_token: process.env.GITEE_TOKEN,
                    tag_name: tagName,
                    name: name,
                    body: body,
                    prerelease: prerelease.toString(),
                    target_commitish: process.env.GITEE_TARGET_COMMITISH
                }
            }).json<{ id: string }>();

            return Promise.all(assets.map(async (asset) => {
                let form = new FormData();
                form.append("access_token", process.env.GITEE_TOKEN);
                form.append("file", asset.data, {
                    "filename": asset.name,
                    "contentType": "application/gzip",
                    "knownLength": asset.data.length
                });

                const request = got(`https://gitee.com/api/v5/repos/${process.env.GITEE_OWNER}/${process.env.GITEE_REPO}/releases/${id}/attach_files`, {
                    method: "POST",
                    ...ofChunkedRequest(form)
                });
                const startTime = Date.now();
                request.on('uploadProgress', ({percent: progress}) => {
                    const estimated = (Date.now() - startTime) / 1000 / progress * (1 - progress);
                    pushUploadingJob(`[GTE] ${asset.name}`, {progress, estimated});
                });

                await request;
            }));
        })(),
        (async () => {
            const {id} = await got(`https://api.cnb.cool/${process.env.CNB_OWNER}/${process.env.CNB_REPO}/-/releases`, {
                method: "POST",
                "json": {
                    "body": body,
                    "draft": false,
                    "make_latest": name,
                    "name": name,
                    "prerelease": prerelease,
                    "tag_name": tagName,
                    "target_commitish": process.env.CNB_TARGET_COMMITISH
                },
                "headers": {
                    "Authorization": "Bearer " + process.env.CNB_TOKEN,
                    "Accept": "application/json",
                    "Content-Type": "application/json;charset=UTF-8"
                }
            }).json<{ id: string }>();

            return Promise.all(assets.map(async (asset) => {
                const {upload_url: uploadURL, verify_url: verifyURL} = await got(
                    `https://api.cnb.cool/${process.env.CNB_OWNER}/${process.env.CNB_REPO}/-/releases/${id}/asset-upload-url`, {
                        method: "POST",
                        "json": {
                            "asset_name": asset.name,
                            "overwrite": true,
                            "size": asset.data.length
                        },
                        "headers": {
                            "Authorization": "Bearer " + process.env.CNB_TOKEN,
                            "Accept": "application/json", // CNB API requires Accept to be exactly 'application/json'.
                            "Content-Type": "application/json;charset=UTF-8"
                        }
                    }
                ).json<{
                    upload_url: string,
                    verify_url: string
                }>();

                let form = new FormData();
                form.append("file", asset.data, {
                    "filename": asset.name,
                    "contentType": "application/gzip",
                    "knownLength": asset.data.length
                });

                const request = got(uploadURL, {
                    method: "POST",
                    ...ofChunkedRequest(form)
                });
                const startTime = Date.now();
                request.on('uploadProgress', ({percent: progress}) => {
                    const estimated = (Date.now() - startTime) / 1000 / progress * (1 - progress);
                    pushUploadingJob(`[CNB] ${asset.name}`, {progress, estimated});
                });

                await request.json()

                await fetch(verifyURL, {
                    method: "POST",
                    headers: {
                        "Authorization": "Bearer " + process.env.CNB_TOKEN,
                        "Accept": "application/json", // CNB API requires Accept to be exactly 'application/json'.
                    }
                }).then(r => r.text()).then(j => console.log(j));
            }));
        })()
    ]);
}
